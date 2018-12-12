﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net.Http;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Resources.Create;
using Microsoft.Health.Fhir.Core.Features.Resources.Delete;
using Microsoft.Health.Fhir.Core.Features.Resources.Get;
using Microsoft.Health.Fhir.Core.Features.Resources.Upsert;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Fhir.Tests.Common.Mocks;
using NSubstitute;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Core.UnitTests.Persistence
{
    public class ResourceHandlerTests
    {
        private readonly IDataStore _dataStore;
        private readonly IConformanceProvider _conformanceProvider;
        private readonly IRawResourceFactory _rawResourceFactory;
        private readonly IResourceWrapperFactory _resourceWrapperFactory;
        private readonly CapabilityStatement _conformanceStatement;
        private readonly IMediator _mediator;

        public ResourceHandlerTests()
        {
            _dataStore = Substitute.For<IDataStore>();
            _conformanceProvider = Substitute.For<ConformanceProviderBase>();

            // TODO: FhirRepository instantiate ResourceDeserializer class directly
            // which will try to deserialize the raw resource. We should mock it as well.
            _rawResourceFactory = Substitute.For<RawResourceFactory>(new FhirJsonSerializer());
            _resourceWrapperFactory = Substitute.For<IResourceWrapperFactory>();
            _resourceWrapperFactory
                .Create(Arg.Any<Resource>(), Arg.Any<bool>())
                .Returns(x => CreateResourceWrapper(x.ArgAt<Resource>(0), x.ArgAt<bool>(1)));

            _conformanceStatement = CapabilityStatementMock.GetMockedCapabilityStatement();
            CapabilityStatementMock.SetupMockResource(_conformanceStatement, ResourceType.Observation, null);
            var observationResource = _conformanceStatement.Rest.First().Resource.Find(x => x.Type == ResourceType.Observation);
            observationResource.ReadHistory = false;
            observationResource.UpdateCreate = true;
            observationResource.Versioning = CapabilityStatement.ResourceVersionPolicy.Versioned;

            CapabilityStatementMock.SetupMockResource(_conformanceStatement, ResourceType.Patient, null);
            var patientResource = _conformanceStatement.Rest.First().Resource.Find(x => x.Type == ResourceType.Patient);
            patientResource.ReadHistory = true;
            patientResource.UpdateCreate = true;
            patientResource.Versioning = CapabilityStatement.ResourceVersionPolicy.VersionedUpdate;

            _conformanceProvider.GetCapabilityStatementAsync().Returns(_conformanceStatement);
            var lazyConformanceProvider = new Lazy<IConformanceProvider>(() => _conformanceProvider);

            var collection = new ServiceCollection();

            collection.Add(x => new CreateResourceHandler(_dataStore, lazyConformanceProvider, _resourceWrapperFactory)).Singleton().AsSelf().AsImplementedInterfaces();
            collection.Add(x => new UpsertResourceHandler(_dataStore, lazyConformanceProvider, _resourceWrapperFactory)).Singleton().AsSelf().AsImplementedInterfaces();
            collection.Add(x => new GetResourceHandler(_dataStore, lazyConformanceProvider, _resourceWrapperFactory)).Singleton().AsSelf().AsImplementedInterfaces();
            collection.Add(x => new DeleteResourceHandler(_dataStore, lazyConformanceProvider, _resourceWrapperFactory)).Singleton().AsSelf().AsImplementedInterfaces();

            ServiceProvider provider = collection.BuildServiceProvider();
            _mediator = new Mediator(type => provider.GetService(type));
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenCreatingAResourceWithAnId_ThenTheIdShouldBeIgnoredAndAnIdShouldBeAssigned()
        {
            var resource = Samples.GetDefaultObservation();
            resource.Id = "id1";
            var wrapper = CreateResourceWrapper(resource, false);

            _dataStore.UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true).Returns(new UpsertOutcome(wrapper, SaveOutcomeType.Created));

            await _mediator.CreateResourceAsync(resource);

            Assert.NotNull(resource.Id);
            Assert.NotEqual("id1", resource.Id);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenSavingAResourceWithNoId_ThenAnIdShouldBeAssigned()
        {
            var resource = Samples.GetDefaultObservation();
            resource.Id = null;

            _dataStore.UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true)
                .Returns(x => new UpsertOutcome(x.ArgAt<ResourceWrapper>(0), SaveOutcomeType.Created));

            await _mediator.UpsertResourceAsync(resource);

            Assert.NotNull(resource.Id);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenSavingAResource_ThenLastUpdatedShouldBeSet()
        {
            var resource = Samples.GetDefaultObservation();
            var instant = DateTimeOffset.UtcNow;

            using (Mock.Property(() => Clock.UtcNowFunc, () => instant))
            {
                _dataStore.UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true)
                    .Returns(x => new UpsertOutcome(x.ArgAt<ResourceWrapper>(0), SaveOutcomeType.Created));

                await _mediator.UpsertResourceAsync(resource);

                Assert.Equal(instant, resource.Meta.LastUpdated);
            }
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenSavingAResource_ThenVersionShouldBeSet()
        {
            var resource = Samples.GetDefaultObservation();

            ResourceWrapper CreateWrapper(ResourceWrapper wrapper)
            {
                var newResource = Samples.GetDefaultObservation();
                newResource.Id = wrapper.ResourceId;
                newResource.VersionId = "version1";
                return CreateResourceWrapper(newResource, false);
            }

            _dataStore.UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true)
                .Returns(x => new UpsertOutcome(CreateWrapper(x.ArgAt<ResourceWrapper>(0)), SaveOutcomeType.Created));

            var outcome = await _mediator.UpsertResourceAsync(resource);

            Assert.Equal("version1", outcome.Resource.VersionId);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenSavingAResourceWithoutETagAndETagIsRequired_ThenPreconditionFailExceptionIsThrown()
        {
            _conformanceStatement.Rest.First().Resource.Find(x => x.Type == ResourceType.Patient).UpdateCreate = false;

            var resource = Samples.GetDefaultPatient();

            await Assert.ThrowsAsync<PreconditionFailedException>(async () => await _mediator.UpsertResourceAsync(resource, null));
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenSavingAResourceWithoutETagAndETagIsRequiredWithUpdateCreate_ThenPreconditionFailExceptionIsThrown()
        {
            var resource = Samples.GetDefaultPatient();

            // Documented for completeness, but under this situation arguably this request should succeed.
            // Versioned-update + UpdateCreate; When no If-Match header is provided, the request should allow create but not update
            await Assert.ThrowsAsync<PreconditionFailedException>(async () => await _mediator.UpsertResourceAsync(resource, null));
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenSavingAResourceWithoutETag_ThenPreconditionFailExceptionIsThrown()
        {
            var resource = Samples.GetDefaultObservation();

            ResourceWrapper CreateWrapper(ResourceWrapper wrapper)
            {
                var newResource = Samples.GetDefaultObservation();
                newResource.Id = wrapper.ResourceId;
                newResource.VersionId = "version1";
                return CreateResourceWrapper(newResource, false);
            }

            _dataStore.UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true)
                .Returns(x => new UpsertOutcome(CreateWrapper(x.ArgAt<ResourceWrapper>(0)), SaveOutcomeType.Updated));

            var outcome = await _mediator.UpsertResourceAsync(resource, null);

            Assert.NotNull(outcome);
            Assert.Equal(resource.Id, outcome.Resource.Id);
            Assert.Equal("version1", outcome.Resource.VersionId);
        }

        [Fact]
        public async Task GivenAFhirMediator_GettingAnResourceThatDoesntExist_ThenANotFoundExceptionIsThrown()
        {
            await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await _mediator.GetResourceAsync(new ResourceKey<Observation>("id1")));

            await _dataStore.Received().GetAsync(Arg.Any<ResourceKey>());
        }

        [Fact]
        public async Task GivenAFhirMediator_GettingAnResourceThatIsDeleted_ThenAGoneExceptionIsThrown()
        {
            var observation = Samples.GetDefaultObservation();
            observation.Id = "id1";

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "id1")).Returns(CreateResourceWrapper(observation, true));

            await Assert.ThrowsAsync<ResourceGoneException>(async () => await _mediator.GetResourceAsync(new ResourceKey<Observation>("id1")));

            await _dataStore.Received().GetAsync(Arg.Any<ResourceKey>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GivenAFhirMediator_WhenDeletingAResourceByVersionId_ThenMethodNotAllowedIsThrown(bool hardDelete)
        {
            await Assert.ThrowsAsync<MethodNotAllowedException>(async () => await _mediator.DeleteResourceAsync(new ResourceKey<Observation>("id1", "version1"), hardDelete));
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenDeletingAResourceThatIsAlreadyDeleted_ThenDoNothing()
        {
            var observation = Samples.GetDefaultObservation();
            observation.Id = "id1";
            observation.Meta = new Meta
            {
                VersionId = "version1",
            };

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "id1")).Returns(CreateResourceWrapper(observation, true));

            ResourceKey resultKey = (await _mediator.DeleteResourceAsync(new ResourceKey<Observation>("id1"), false)).ResourceKey;

            await _dataStore.DidNotReceive().UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true);

            Assert.Equal(observation.Id, resultKey.Id);
            Assert.Equal(observation.Meta.VersionId, resultKey.VersionId);
            Assert.Equal("Observation", resultKey.ResourceType);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenDeletingAResourceThatDoesNotExist_ThenDoNothing()
        {
            var observation = Samples.GetDefaultObservation();
            observation.Id = "id1";
            observation.Meta = new Meta
            {
                VersionId = "version1",
            };

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "id1")).Returns((ResourceWrapper)null);

            ResourceKey resultKey = (await _mediator.DeleteResourceAsync(new ResourceKey<Observation>("id1"), false)).ResourceKey;

            await _dataStore.DidNotReceive().UpsertAsync(Arg.Any<ResourceWrapper>(), Arg.Any<WeakETag>(), true, true);

            Assert.Equal(observation.Id, resultKey.Id);
            Assert.Null(resultKey.VersionId);
            Assert.Equal("Observation", resultKey.ResourceType);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenHardDeleting_ThenItWillBeHardDeleted()
        {
            ResourceKey resourceKey = new ResourceKey<Observation>("id1");

            ResourceKey resultKey = (await _mediator.DeleteResourceAsync(resourceKey, true)).ResourceKey;

            await _dataStore.Received(1).HardDeleteAsync(resourceKey);

            Assert.NotNull(resultKey);
            Assert.Equal(resourceKey.Id, resultKey.Id);
            Assert.Null(resultKey.VersionId);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenConfiguredWithoutReadHistory_ThenReturnMethodNotAllowedForOldVersionObservation()
        {
            var observation = Samples.GetDefaultObservation();
            observation.Id = "readDataObservation";

            var history = CreateMockResourceWrapper(observation, false);
            history.IsHistory.Returns(true);

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "readDataObservation" && x.VersionId == "history")).Returns(history);

            await Assert.ThrowsAsync<MethodNotAllowedException>(async () =>
            {
                await _mediator.GetResourceAsync(new ResourceKey("Observation", "readDataObservation", "history"));
            });
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenConfiguredWithoutReadHistory_ThenReturnObservationForLatestVersion()
        {
            var observation = Samples.GetDefaultObservation();
            observation.Id = "readDataObservation";

            var latest = CreateMockResourceWrapper(observation, false);
            latest.IsHistory.Returns(false);

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "readDataObservation")).Returns(latest);

            Observation result = await _mediator.GetResourceAsync(new ResourceKey("Observation", "readDataObservation", "latest")) as Observation;

            Assert.NotNull(result);
            Assert.Equal(observation.Id, result.Id);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenConfiguredWithReadHistory_ThenReturnsPatientWithOldVersion()
        {
            var patient = Samples.GetDefaultPatient();

            var history = CreateMockResourceWrapper(patient, false);
            history.IsHistory.Returns(true);

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "readDataPatient" && x.VersionId == "history")).Returns(history);

            Patient result = await _mediator.GetResourceAsync(new ResourceKey("Patient", "readDataPatient", "history")) as Patient;

            Assert.NotNull(result);
            Assert.Equal(patient.BirthDate, result.BirthDate);
            Assert.Equal(patient.Gender, result.Gender);
        }

        [Fact]
        public async Task GivenAFhirMediator_WhenConfiguredWithoutReadHistory_ThenReturnsPatientWithLatestVersion()
        {
            var patient = Samples.GetDefaultPatient();

            var latest = CreateMockResourceWrapper(patient, false);
            latest.IsHistory.Returns(false);

            _dataStore.GetAsync(Arg.Is<ResourceKey>(x => x.Id == "readDataPatient")).Returns(latest);

            Patient result = await _mediator.GetResourceAsync(new ResourceKey("Patient", "readDataPatient", "latest")) as Patient;

            Assert.NotNull(result);
            Assert.Equal(patient.BirthDate, result.BirthDate);
            Assert.Equal(patient.Gender, result.Gender);
        }

        private ResourceWrapper CreateResourceWrapper(Resource resource, bool isDeleted)
        {
            return new ResourceWrapper(
                resource,
                _rawResourceFactory.Create(resource),
                new ResourceRequest("http://fhir", HttpMethod.Post),
                isDeleted,
                null,
                null,
                null);
        }

        private ResourceWrapper CreateMockResourceWrapper(Resource resource, bool isDeleted)
        {
            return Substitute.For<ResourceWrapper>(
                resource,
                _rawResourceFactory.Create(resource),
                new ResourceRequest("http://fhir", HttpMethod.Put),
                isDeleted,
                null,
                null,
                null);
        }
    }
}
