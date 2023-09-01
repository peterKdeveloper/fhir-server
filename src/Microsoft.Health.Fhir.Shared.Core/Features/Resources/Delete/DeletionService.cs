// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Health.Abstractions.Exceptions;
using Microsoft.Health.Core.Features.Audit;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Audit;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Messages.Delete;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;

namespace Microsoft.Health.Fhir.Core.Features.Persistence
{
    public class DeletionService : IDeletionService
    {
        private readonly IResourceWrapperFactory _resourceWrapperFactory;
        private readonly Lazy<IConformanceProvider> _conformanceProvider;
        private readonly IFhirDataStore _fhirDataStore;
        private readonly ISearchService _searchService;
        private readonly ResourceIdProvider _resourceIdProvider;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly FhirRequestContextAccessor _contextAccessor;
        private readonly IAuditLogger _auditLogger;

        internal const string DefaultCallerAgent = "Microsoft.Health.Fhir.Server";

        public DeletionService(
            IResourceWrapperFactory resourceWrapperFactory,
            Lazy<IConformanceProvider> conformanceProvider,
            IFhirDataStore fhirDataStore,
            ISearchService searchService,
            ResourceIdProvider resourceIdProvider,
            FhirRequestContextAccessor contextAccessor,
            IAuditLogger auditLogger)
        {
            _resourceWrapperFactory = EnsureArg.IsNotNull(resourceWrapperFactory, nameof(resourceWrapperFactory));
            _conformanceProvider = EnsureArg.IsNotNull(conformanceProvider, nameof(conformanceProvider));
            _fhirDataStore = EnsureArg.IsNotNull(fhirDataStore, nameof(fhirDataStore));
            _searchService = EnsureArg.IsNotNull(searchService, nameof(searchService));
            _resourceIdProvider = EnsureArg.IsNotNull(resourceIdProvider, nameof(resourceIdProvider));
            _contextAccessor = EnsureArg.IsNotNull(contextAccessor, nameof(contextAccessor));
            _auditLogger = EnsureArg.IsNotNull(auditLogger, nameof(auditLogger));

            _retryPolicy = Policy
                .Handle<RequestRateExceededException>()
                .WaitAndRetryAsync(3, count => TimeSpan.FromSeconds(Math.Pow(2, count) + RandomNumberGenerator.GetInt32(0, 5)));
        }

        public async Task<ResourceKey> DeleteAsync(DeleteResourceRequest request, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            ResourceKey key = request.ResourceKey;

            if (!string.IsNullOrEmpty(key.VersionId))
            {
                throw new MethodNotAllowedException(Core.Resources.DeleteVersionNotAllowed);
            }

            string version = null;

            switch (request.DeleteOperation)
            {
                case DeleteOperation.SoftDelete:
                    ResourceWrapper deletedWrapper = CreateSoftDeletedWrapper(key.ResourceType, request.ResourceKey.Id);

                    bool keepHistory = await _conformanceProvider.Value.CanKeepHistory(key.ResourceType, cancellationToken);

                    UpsertOutcome result = await _retryPolicy.ExecuteAsync(async () => await _fhirDataStore.UpsertAsync(new ResourceWrapperOperation(deletedWrapper, true, keepHistory, null, false, false, bundleResourceContext: request.BundleResourceContext), cancellationToken));

                    version = result?.Wrapper.Version;
                    break;
                case DeleteOperation.HardDelete:
                case DeleteOperation.PurgeHistory:
                    await _retryPolicy.ExecuteAsync(async () => await _fhirDataStore.HardDeleteAsync(key, request.DeleteOperation == DeleteOperation.PurgeHistory, cancellationToken));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request));
            }

            return new ResourceKey(key.ResourceType, key.Id, version);
        }

        public async Task<IReadOnlySet<string>> DeleteMultipleAsync(ConditionalDeleteResourceRequest request, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            var searchCount = 1000;

            (IReadOnlyCollection<SearchResultEntry> matchedResults, string ct) = await _searchService.ConditionalSearchAsync(request.ResourceType, request.ConditionalParameters, cancellationToken, request.DeleteAll ? searchCount : request.MaxDeleteCount);

            var itemsDeleted = new HashSet<string>();

            // Delete the matched results...
            try
            {
                while (matchedResults.Any() || !string.IsNullOrEmpty(ct))
                {
                    IReadOnlyCollection<SearchResultEntry> resultsToDelete =
                        request.DeleteAll ? matchedResults : matchedResults
                            .Take(Math.Max(request.MaxDeleteCount.GetValueOrDefault() - itemsDeleted.Count, 0))
                            .ToArray();

                    CreateAuditLog(request.ResourceType, request.DeleteOperation, false, resultsToDelete.Select((item) => item.Resource.ResourceId));

                    if (request.DeleteOperation == DeleteOperation.SoftDelete)
                    {
                        bool keepHistory = await _conformanceProvider.Value.CanKeepHistory(request.ResourceType, cancellationToken);
                        ResourceWrapperOperation[] softDeletes = resultsToDelete.Select(item =>
                        {
                            ResourceWrapper deletedWrapper = CreateSoftDeletedWrapper(request.ResourceType, item.Resource.ResourceId);
                            return new ResourceWrapperOperation(deletedWrapper, true, keepHistory, null, false, false, bundleResourceContext: request.BundleResourceContext);
                        }).ToArray();

                        try
                        {
                            await _fhirDataStore.MergeAsync(softDeletes, cancellationToken);
                        }
                        catch (IncompleteOperationException<IDictionary<DataStoreOperationIdentifier, DataStoreOperationOutcome>> ex)
                        {
                            foreach (string id in ex.PartialResults.Select(item => item.Key.Id))
                            {
                                itemsDeleted.Add(id);
                            }

                            CreateAuditLog(request.ResourceType, request.DeleteOperation, true, itemsDeleted.ToList());

                            throw;
                        }

                        foreach (string id in itemsDeleted.Concat(resultsToDelete.Select(item => item.Resource.ResourceId)))
                        {
                            itemsDeleted.Add(id);
                        }
                    }
                    else
                    {
                        var parallelBag = new ConcurrentBag<string>();
                        try
                        {
                            await Parallel.ForEachAsync(resultsToDelete, cancellationToken, async (item, innerCt) =>
                            {
                                await _retryPolicy.ExecuteAsync(async () => await _fhirDataStore.HardDeleteAsync(new ResourceKey(item.Resource.ResourceTypeName, item.Resource.ResourceId), request.DeleteOperation == DeleteOperation.PurgeHistory, innerCt));
                                parallelBag.Add(item.Resource.ResourceId);
                            });
                        }
                        finally
                        {
                            foreach (string item in parallelBag)
                            {
                                itemsDeleted.Add(item);
                            }
                        }
                    }

                    CreateAuditLog(request.ResourceType, request.DeleteOperation, true, itemsDeleted.ToList());

                    if (!string.IsNullOrEmpty(ct) && (request.MaxDeleteCount - itemsDeleted.Count > 0 || request.DeleteAll))
                    {
                        (matchedResults, ct) = await _searchService.ConditionalSearchAsync(
                            request.ResourceType,
                            request.ConditionalParameters,
                            cancellationToken,
                            request.DeleteAll ? searchCount : request.MaxDeleteCount - itemsDeleted.Count,
                            ct);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IncompleteOperationException<IReadOnlySet<string>>(ex, itemsDeleted);
            }

            return itemsDeleted;
        }

        private ResourceWrapper CreateSoftDeletedWrapper(string resourceType, string resourceId)
        {
            EnsureArg.IsNotNullOrEmpty(resourceType, nameof(resourceType));
            EnsureArg.IsNotNullOrEmpty(resourceId, nameof(resourceId));

            ISourceNode emptySourceNode = FhirJsonNode.Create(
                JObject.FromObject(
                    new
                    {
                        resourceType,
                        id = resourceId,
                    }));

            ResourceWrapper deletedWrapper = _resourceWrapperFactory.CreateResourceWrapper(emptySourceNode.ToPoco<Resource>(), _resourceIdProvider, deleted: true, keepMeta: false);
            return deletedWrapper;
        }

        private void CreateAuditLog(string resourceType, DeleteOperation operation, bool complete, IEnumerable<string> items, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            AuditAction action = complete ? AuditAction.Executed : AuditAction.Executing;
            var context = _contextAccessor.RequestContext;
            var deleteAdditionalProperties = new Dictionary<string, string>();
            deleteAdditionalProperties["Affected Items"] = items.Aggregate(
                (aggregate, item) =>
                {
                    aggregate += ", " + item;
                    return aggregate;
                });

            _auditLogger.LogAudit(
                auditAction: action,
                operation: operation.ToString(),
                resourceType: resourceType,
                requestUri: context.Uri,
                statusCode: statusCode,
                correlationId: context.CorrelationId,
                callerIpAddress: string.Empty,
                callerClaims: null,
                customHeaders: null,
                operationType: string.Empty,
                callerAgent: DefaultCallerAgent,
                additionalProperties: deleteAdditionalProperties);
        }
    }
}
