﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using MediatR;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Messages.Create;
using Microsoft.Health.Fhir.Core.Messages.Upsert;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Search.Parameters
{
    public class CreateSearchParameterBehavior<TCreateResourceRequest, TUpsertResourceResponse> : IPipelineBehavior<CreateResourceRequest, UpsertResourceResponse>,
        IPipelineBehavior<UpsertResourceRequest, UpsertResourceResponse>
    {
        private ISearchParameterUtilities _searchParameterUtitliies;
        private IFhirDataStore _fhirDataStore;

        public CreateSearchParameterBehavior(ISearchParameterUtilities searchParameterUtilities, IFhirDataStore fhirDataStore)
        {
            EnsureArg.IsNotNull(searchParameterUtilities, nameof(searchParameterUtilities));
            EnsureArg.IsNotNull(fhirDataStore, nameof(fhirDataStore));

            _searchParameterUtitliies = searchParameterUtilities;
            _fhirDataStore = fhirDataStore;
        }

        public async Task<UpsertResourceResponse> Handle(CreateResourceRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<UpsertResourceResponse> next)
        {
            var response = await next();

            if (request.Resource.InstanceType.Equals(KnownResourceTypes.SearchParameter, StringComparison.Ordinal))
            {
                // Once the SearchParameter resource is committed to the data store, we can update the in
                // memory SearchParameterDefinitionManager, and persist the status to the data store
                await _searchParameterUtitliies.AddSearchParameterAsync(request.Resource.Instance);
            }

            return response;
        }

        public async Task<UpsertResourceResponse> Handle(UpsertResourceRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<UpsertResourceResponse> next)
        {
            var resourceKey = new ResourceKey(request.Resource.InstanceType, request.Resource.Id, request.Resource.VersionId);
            ResourceWrapper prevSearchParamResource = null;

            if (request.Resource.InstanceType.Equals(KnownResourceTypes.SearchParameter, StringComparison.Ordinal))
            {
                prevSearchParamResource = await _fhirDataStore.GetAsync(resourceKey, cancellationToken);
            }

            var response = await next();

            if (request.Resource.InstanceType.Equals(KnownResourceTypes.SearchParameter, StringComparison.Ordinal))
            {
                // Once the SearchParameter resource is update in the data store, we will update
                // the metadata in the SearchParameterDefinitionManager
                await _searchParameterUtitliies.UpdateSearchParameterAsync(request.Resource.Instance, prevSearchParamResource.RawResource);
            }

            return response;
        }
    }
}
