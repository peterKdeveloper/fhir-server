// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Routing;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Features.Search.Access;
using Microsoft.Health.Fhir.Core.Features.Search.Converters;
using Microsoft.Health.Fhir.Core.Features.Search.Expressions.Parsers;
using Microsoft.Health.Fhir.Core.Features.Search.InMemory;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Subscriptions.Models;
using Microsoft.Health.Fhir.Subscriptions.Operations;
using Microsoft.Health.Fhir.Subscriptions.Persistence;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.JobManagement;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Search.InMemory
{
    public class SubscriptionsOrchestratorJobTests : IAsyncLifetime
    {
        private ISearchIndexer _searchIndexer;
        private IQueueClient _mockQueueClient = Substitute.For<IQueueClient>();
        private ITransactionDataStore _transactionDataStore = Substitute.For<ITransactionDataStore>();
        private ISearchOptionsFactory _searchOptionsFactory;
        private IQueryStringParser _queryStringParser;
        private ISubscriptionManager _subscriptionManager = Substitute.For<ISubscriptionManager>();
        private IResourceDeserializer _resourceDeserializer;
        private IExpressionParser _expressionParser;

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            var searchQueryInterpreter = new SearchQueryInterpreter();

            var fixture = new SearchParameterFixtureData();
            var manager = await fixture.GetSearchDefinitionManagerAsync();

            var fhirRequestContextAccessor = new FhirRequestContextAccessor();
            var supportedSearchParameterDefinitionManager = new SupportedSearchParameterDefinitionManager(manager);
            var searchableSearchParameterDefinitionManager = new SearchableSearchParameterDefinitionManager(manager, fhirRequestContextAccessor);
            var typedElementToSearchValueConverterManager = GetTypeConverterAsync().Result;

            var referenceParser = new ReferenceSearchValueParser(fhirRequestContextAccessor);
            var referenceToElementResolver = new LightweightReferenceToElementResolver(referenceParser, ModelInfoProvider.Instance);
            var modelInfoProvider = ModelInfoProvider.Instance;
            var logger = Substitute.For<ILogger<TypedElementSearchIndexer>>();

            _searchIndexer = new TypedElementSearchIndexer(supportedSearchParameterDefinitionManager, typedElementToSearchValueConverterManager, referenceToElementResolver, modelInfoProvider, logger);

            _transactionDataStore.GetResourcesByTransactionIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(x =>
            {
                var resourceWrappers = new List<ResourceWrapper>();
                var resource = Samples.GetDefaultPatient().UpdateId("123");
                var rawResourceFactory = new RawResourceFactory(new FhirJsonSerializer());
                ResourceWrapper resourceWrapper = new ResourceWrapper(resource, rawResourceFactory.Create(resource, keepMeta: true), new ResourceRequest(HttpMethod.Post, "http://fhir"), false, null, null, null);
                resourceWrappers.Add(resourceWrapper);
                return resourceWrappers.AsReadOnly();
            });
            _queryStringParser = new TestQueryStringParser();
            var options = new OptionsWrapper<CoreFeatureConfiguration>(new CoreFeatureConfiguration());
            _expressionParser = new ExpressionParser(() => searchableSearchParameterDefinitionManager, new SearchParameterExpressionParser(referenceParser));

            _searchOptionsFactory = new SearchOptionsFactory(
                _expressionParser, () => manager, options, fhirRequestContextAccessor, Substitute.For<ISortingValidator>(),  new ExpressionAccessControl(fhirRequestContextAccessor), NullLogger<SearchOptionsFactory>.Instance);

            _subscriptionManager.GetActiveSubscriptionsAsync(Arg.Any<CancellationToken>()).Returns(x =>
            {
                var subscriptionInfo = SubscriptionManager.ConvertToInfo(Samples.GetJsonSample("Subscription"));
                var subscriptionInfoList = new List<SubscriptionInfo>
                {
                    subscriptionInfo,
                };
                return subscriptionInfoList.AsReadOnly();
            });
            var fhirJsonParser = new FhirJsonParser();
            _resourceDeserializer = Deserializers.ResourceDeserializer;
        }

        protected async Task<ITypedElementToSearchValueConverterManager> GetTypeConverterAsync()
        {
            FhirTypedElementToSearchValueConverterManager fhirTypedElementToSearchValueConverterManager = await SearchParameterFixtureData.GetFhirTypedElementToSearchValueConverterManagerAsync();
            return fhirTypedElementToSearchValueConverterManager;
        }

        [Fact]
        public async Task GivenASubscriptionOrchestrator_WhenExecuting_ThenASubscriptionProcessingJobIsQueued()
        {
            var orchestrator = new SubscriptionsOrchestratorJob(_mockQueueClient, _transactionDataStore, _searchOptionsFactory, _queryStringParser, _subscriptionManager, _resourceDeserializer, _searchIndexer);
            var definition = new SubscriptionJobDefinition(JobType.SubscriptionsOrchestrator) { TransactionId = 1, TypeId = 1 };
            var jobInfo = new JobInfo() { Status = JobStatus.Created, Definition = JsonConvert.SerializeObject(definition), GroupId = 1 };

            await orchestrator.ExecuteAsync(jobInfo, default);
            await _mockQueueClient.Received().EnqueueAsync((byte)QueueType.Subscriptions, Arg.Is<string[]>(x => x.Length == 1), 1, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }
    }
}
