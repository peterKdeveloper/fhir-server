﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Operations.Export;
using Microsoft.Health.Fhir.Core.Features.Operations.Export.ExportDestinationClient;
using Microsoft.Health.Fhir.Core.Features.Operations.Export.Models;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Features.SecretStore;
using Microsoft.Health.Fhir.Core.UnitTests.Extensions;
using Microsoft.Health.Fhir.Tests.Common;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Operations.Export
{
    public class ExportJobTaskTests
    {
        private static readonly WeakETag _weakETag = WeakETag.FromVersionId("0");

        private ExportJobRecord _exportJobRecord;
        private IExportDestinationClientFactory _exportDestinationClientFactory = Substitute.For<IExportDestinationClientFactory>();
        private InMemoryExportDestinationClient _inMemoryDestinationClient = new InMemoryExportDestinationClient();

        private readonly IFhirOperationDataStore _fhirOperationDataStore = Substitute.For<IFhirOperationDataStore>();
        private readonly ISecretStore _secretStore = Substitute.For<ISecretStore>();
        private readonly ExportJobConfiguration _exportJobConfiguration = new ExportJobConfiguration();
        private readonly ISearchService _searchService = Substitute.For<ISearchService>();
        private readonly IResourceToByteArraySerializer _resourceToByteArraySerializer = Substitute.For<IResourceToByteArraySerializer>();

        private readonly ExportJobTask _exportJobTask;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _cancellationToken;

        private ExportJobOutcome _lastExportJobOutcome;

        public ExportJobTaskTests()
        {
            _cancellationToken = _cancellationTokenSource.Token;
            _exportJobRecord = new ExportJobRecord(
                new Uri("https://localhost/ExportJob/"),
                "Patient",
                "hash");

            _fhirOperationDataStore.UpdateExportJobAsync(_exportJobRecord, _weakETag, _cancellationToken).Returns(x =>
            {
                _lastExportJobOutcome = new ExportJobOutcome(_exportJobRecord, _weakETag);

                return _lastExportJobOutcome;
            });

            _secretStore.GetSecretAsync(Arg.Any<string>(), _cancellationToken).Returns(x => new SecretWrapper(x.ArgAt<string>(0), "{\"destinationType\": \"in-memory\"}"));

            _exportDestinationClientFactory.Create("in-memory").Returns(_inMemoryDestinationClient);

            _resourceToByteArraySerializer.Serialize(Arg.Any<ResourceWrapper>()).Returns(x => Encoding.UTF8.GetBytes(x.ArgAt<ResourceWrapper>(0).ResourceId));

            _exportJobTask = new ExportJobTask(
                () => _fhirOperationDataStore.CreateMockScope(),
                _secretStore,
                Options.Create(_exportJobConfiguration),
                () => _searchService.CreateMockScope(),
                _resourceToByteArraySerializer,
                _exportDestinationClientFactory,
                NullLogger<ExportJobTask>.Instance);
        }

        [Fact]
        public async Task GivenAJob_WhenExecuted_ThenCorrectSearchIsPerformed()
        {
            bool capturedSearch = false;

            _exportJobConfiguration.MaximumNumberOfResourcesPerQuery = 1;

            // Check to make sure the search is performed with the correct conditions.
            _searchService.SearchAsync(
                _exportJobRecord.ResourceType,
                Arg.Is(CreateQueryParametersExpression(null)),
                _cancellationToken)
                .Returns(x =>
                {
                    capturedSearch = true;

                    return CreateSearchResult();
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            Assert.True(capturedSearch);
        }

        [Fact]
        public async Task GivenThereAreMoreSearchResults_WhenExecuted_ThenCorrectSearchIsPerformed()
        {
            const string continuationToken = "ct";

            _exportJobConfiguration.MaximumNumberOfResourcesPerQuery = 1;

            // First search returns a search result with continuation token.
            _searchService.SearchAsync(
                _exportJobRecord.ResourceType,
                Arg.Is(CreateQueryParametersExpression(null)),
                _cancellationToken)
                .Returns(CreateSearchResult(continuationToken: continuationToken));

            bool capturedSearch = false;

            // Second search returns a search result without continuation token.
            _searchService.SearchAsync(
                _exportJobRecord.ResourceType,
                Arg.Is(CreateQueryParametersExpression(continuationToken)),
                _cancellationToken)
                .Returns(x =>
                {
                    capturedSearch = true;

                    return CreateSearchResult();
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            Assert.True(capturedSearch);
        }

        private Expression<Predicate<IReadOnlyList<Tuple<string, string>>>> CreateQueryParametersExpression(string continuationToken)
        {
            return arg => arg != null && Tuple.Create("ct", continuationToken).Equals(arg[0]) && Tuple.Create("_count", "1").Equals(arg[1]) && Tuple.Create("_lastUpdated", $"le{_exportJobRecord.QueuedTime.ToString("o")}").Equals(arg[2]);
        }

        [Fact]
        public async Task GivenSearchSucceeds_WhenExecuted_ThenJobStatusShouldBeUpdatedToCompleted()
        {
            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(x => CreateSearchResult());

            DateTimeOffset endTimestamp = DateTimeOffset.UtcNow;

            using (Mock.Property(() => Clock.UtcNowFunc, () => endTimestamp))
            {
                await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);
            }

            Assert.NotNull(_lastExportJobOutcome);
            Assert.Equal(OperationStatus.Completed, _lastExportJobOutcome.JobRecord.Status);
            Assert.Equal(endTimestamp, _lastExportJobOutcome.JobRecord.EndTime);
        }

        [Fact]
        public async Task GivenSearchFailed_WhenExecuted_ThenJobStatusShouldBeUpdatedToFailed()
        {
            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns<SearchResult>(x =>
                {
                    throw new Exception();
                });

            DateTimeOffset endTimestamp = DateTimeOffset.UtcNow;

            using (Mock.Property(() => Clock.UtcNowFunc, () => endTimestamp))
            {
                await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);
            }

            Assert.NotNull(_lastExportJobOutcome);
            Assert.Equal(OperationStatus.Failed, _lastExportJobOutcome.JobRecord.Status);
            Assert.Equal(endTimestamp, _lastExportJobOutcome.JobRecord.EndTime);
        }

        [Theory]
        [InlineData(0, null)] // Because it fails to perform the 1st search, the file will not be created.
        [InlineData(1, "")] // Because it fails to perform the 2nd search, the file is created but nothing is committed.
        [InlineData(2, "")] // Because it fails to perform the 3rd search, the file is created but nothing is committed.
        [InlineData(3, "012")] // Because it fails to perform the 4th search, the file is created and the first 3 pages are committed.
        [InlineData(4, "012")] // Because it fails to perform the 5th search, the file is created and the first 3 pages are committed.
        [InlineData(5, "012")] // Because it fails to perform the 6th search, the file is created and the first 3 pages are committed.
        [InlineData(6, "012345")] // Because it fails to perform the 7th search, the file is created and the first 6 pages are committed.
        public async Task GivenVariousNumberOfSuccessfulSearch_WhenExecuted_ThenItShouldCommitAtScheduledPage(int numberOfSuccessfulPages, string expectedIds)
        {
            _exportJobConfiguration.NumberOfPagesPerCommit = 3;

            int numberOfCalls = 0;

            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(x =>
                {
                    int count = numberOfCalls++;

                    if (count == numberOfSuccessfulPages)
                    {
                        throw new Exception();
                    }

                    return CreateSearchResult(
                        new[]
                        {
                            new ResourceWrapper(
                                count.ToString(CultureInfo.InvariantCulture),
                                "1",
                                "Patient",
                                new RawResource("data", Core.Models.FhirResourceFormat.Json),
                                null,
                                DateTimeOffset.MinValue,
                                false,
                                null,
                                null,
                                null),
                        },
                        continuationToken: "ct");
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            string actualIds = _inMemoryDestinationClient.GetExportedData(new Uri("Patient.ndjson", UriKind.Relative));

            Assert.Equal(expectedIds, actualIds);
        }

        [Fact]
        public async Task GivenNumberOfSearch_WhenExecuted_ThenItShouldCommitOneLastTime()
        {
            _exportJobConfiguration.NumberOfPagesPerCommit = 3;

            SearchResult searchResultWithContinuationToken = CreateSearchResult(continuationToken: "ct");

            int numberOfCalls = 0;

            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(x =>
                {
                    int count = numberOfCalls++;

                    if (count == 5)
                    {
                        return CreateSearchResult();
                    }

                    return CreateSearchResult(
                        new[]
                        {
                            new ResourceWrapper(
                                count.ToString(CultureInfo.InvariantCulture),
                                "1",
                                "Patient",
                                new RawResource("data", Core.Models.FhirResourceFormat.Json),
                                null,
                                DateTimeOffset.MinValue,
                                false,
                                null,
                                null,
                                null),
                        },
                        continuationToken: "ct");
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            string actualIds = _inMemoryDestinationClient.GetExportedData(new Uri("Patient.ndjson", UriKind.Relative));

            // All of the ids should be present since it should have committed one last time after all the results were exported.
            Assert.Equal("01234", actualIds);
        }

        [Fact]
        public async Task GivenDeleteSecretFailed_WhenExecuted_ThenJobStatusShouldBeUpdatedToCompleted()
        {
            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(CreateSearchResult());

            _secretStore.DeleteSecretAsync(Arg.Any<string>(), _cancellationToken).Returns<SecretWrapper>(_ => throw new Exception());

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            Assert.NotNull(_lastExportJobOutcome);
            Assert.Equal(OperationStatus.Completed, _lastExportJobOutcome.JobRecord.Status);
        }

        [Fact]
        public async Task GivenAnExportJobToResume_WhenExecuted_ThenItShouldExportAllRecordsAsExpected()
        {
            // We are using the SearchService to throw an exception in order to simulate the export job task
            // "crashing" while in the middle of the process.
            _exportJobConfiguration.NumberOfPagesPerCommit = 2;

            int numberOfCalls = 0;
            int numberOfSuccessfulPages = 2;

            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(x =>
                {
                    int count = numberOfCalls;

                    if (count == numberOfSuccessfulPages)
                    {
                        throw new Exception();
                    }

                    numberOfCalls++;
                    return CreateSearchResult(
                        new[]
                        {
                            new ResourceWrapper(
                                count.ToString(CultureInfo.InvariantCulture),
                                "1",
                                "Patient",
                                new RawResource("data", Core.Models.FhirResourceFormat.Json),
                                null,
                                DateTimeOffset.MinValue,
                                false,
                                null,
                                null,
                                null),
                        },
                        continuationToken: "ct");
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            string exportedIds = _inMemoryDestinationClient.GetExportedData(new Uri("Patient.ndjson", UriKind.Relative));

            Assert.Equal("01", exportedIds);
            Assert.NotNull(_exportJobRecord.Progress);

            // We create a new export job task here to simulate the worker picking up the "old" export job record
            // and resuming the export process. The export destination client contains data that has
            // been committed up until the "crash".
            _inMemoryDestinationClient = new InMemoryExportDestinationClient();
            _exportDestinationClientFactory.Create("in-memory").Returns(_inMemoryDestinationClient);
            var secondExportJobTask = new ExportJobTask(
                () => _fhirOperationDataStore.CreateMockScope(),
                _secretStore,
                Options.Create(_exportJobConfiguration),
                () => _searchService.CreateMockScope(),
                _resourceToByteArraySerializer,
                _exportDestinationClientFactory,
                NullLogger<ExportJobTask>.Instance);

            numberOfSuccessfulPages = 5;
            await secondExportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            exportedIds = _inMemoryDestinationClient.GetExportedData(new Uri("Patient.ndjson", UriKind.Relative));
            Assert.Equal("23", exportedIds);
        }

        [Fact]
        public async Task GivenTaskIsRunning_WhenCancelled_ThenOutputShouldUpdated()
        {
            var updatedETag = WeakETag.FromVersionId("updated");

            _exportJobConfiguration.NumberOfPagesPerCommit = 2;

            SearchResult searchResultWithContinuationToken = CreateSearchResult(continuationToken: "ct");

            int numberOfCalls = 0;

            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(x =>
                {
                    int count = numberOfCalls++;

                    if (count == 7)
                    {
                        return CreateSearchResult();
                    }

                    return CreateSearchResult(
                        new[]
                        {
                            new ResourceWrapper(
                                count.ToString(CultureInfo.InvariantCulture),
                                "1",
                                "Patient",
                                new RawResource("data", Core.Models.FhirResourceFormat.Json),
                                null,
                                DateTimeOffset.MinValue,
                                false,
                                null,
                                null,
                                null),
                        },
                        continuationToken: "ct");
                });

            int numberOfDatabaseCalls = 0;
            int capturedLastProcessedCount = 0;
            ExportJobRecord capturedExportJobRecord = null;
            WeakETag capturedWeakETag = null;

            _fhirOperationDataStore.UpdateExportJobAsync(Arg.Any<ExportJobRecord>(), Arg.Any<WeakETag>(), _cancellationToken).Returns(
                x =>
                {
                    int count = numberOfDatabaseCalls++;

                    if (count == 2)
                    {
                        // Setup the call to get the simulated canceled job record.
                        var newExportJobRecord = new ExportJobRecord(capturedExportJobRecord.RequestUri, capturedExportJobRecord.ResourceType, capturedExportJobRecord.Hash, null)
                        {
                            Status = OperationStatus.Cancelled,
                        };

                        var updatedExportJobOutcome = new ExportJobOutcome(newExportJobRecord, updatedETag);

                        _fhirOperationDataStore.GetExportJobByIdAsync(_exportJobRecord.Id, _cancellationToken).Returns(updatedExportJobOutcome);

                        // Simulate the job being canceled by throwing the exception.
                        throw new JobConflictException();
                    }
                    else if (count == 3)
                    {
                        // We should have gotten another update.
                        capturedExportJobRecord = x.ArgAt<ExportJobRecord>(0);
                        capturedWeakETag = x.ArgAt<WeakETag>(1);
                    }
                    else
                    {
                        capturedExportJobRecord = x.ArgAt<ExportJobRecord>(0);

                        capturedLastProcessedCount = capturedExportJobRecord.Output["Patient"].Count;
                    }

                    return new ExportJobOutcome(capturedExportJobRecord, x.ArgAt<WeakETag>(1));
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            Assert.NotNull(capturedExportJobRecord);
            Assert.Equal(OperationStatus.Cancelled, capturedExportJobRecord.Status);
            Assert.Same(updatedETag, capturedWeakETag);

            int updatedCount = capturedExportJobRecord.Output["Patient"].Count;

            // The job was canceled. The output should be updated to reflect the latest committed count.
            Assert.NotEqual(capturedLastProcessedCount, updatedCount);
            Assert.Equal(6, updatedCount);
        }

        [Fact]
        public async Task GivenTaskIsRunning_WhenJobRecordUpdatedExternally_ThenJobRecordShouldNotBeUpdated()
        {
            var updatedETag = WeakETag.FromVersionId("updated");

            _exportJobConfiguration.NumberOfPagesPerCommit = 2;

            SearchResult searchResultWithContinuationToken = CreateSearchResult(continuationToken: "ct");

            int numberOfCalls = 0;

            _searchService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Tuple<string, string>>>(),
                _cancellationToken)
                .Returns(x =>
                {
                    int count = numberOfCalls++;

                    if (count == 7)
                    {
                        return CreateSearchResult();
                    }

                    return CreateSearchResult(
                        new[]
                        {
                            new ResourceWrapper(
                                count.ToString(CultureInfo.InvariantCulture),
                                "1",
                                "Patient",
                                new RawResource("data", Core.Models.FhirResourceFormat.Json),
                                null,
                                DateTimeOffset.MinValue,
                                false,
                                null,
                                null,
                                null),
                        },
                        continuationToken: "ct");
                });

            int numberOfDatabaseCalls = 0;

            _fhirOperationDataStore.UpdateExportJobAsync(Arg.Any<ExportJobRecord>(), Arg.Any<WeakETag>(), _cancellationToken).Returns(
                x =>
                {
                    int count = numberOfDatabaseCalls++;

                    ExportJobRecord exportJobRecord = x.ArgAt<ExportJobRecord>(0);

                    if (count == 2)
                    {
                        // Setup the call to get the simulated canceled job record.
                        var newExportJobRecord = new ExportJobRecord(exportJobRecord.RequestUri, exportJobRecord.ResourceType, exportJobRecord.Hash, null)
                        {
                            Status = OperationStatus.Failed,
                        };

                        var updatedExportJobOutcome = new ExportJobOutcome(newExportJobRecord, updatedETag);

                        _fhirOperationDataStore.GetExportJobByIdAsync(_exportJobRecord.Id, _cancellationToken).Returns(updatedExportJobOutcome);

                        _fhirOperationDataStore.ClearReceivedCalls();

                        // Simulate the job being canceled by throwing the exception.
                        throw new JobConflictException();
                    }

                    return new ExportJobOutcome(exportJobRecord, x.ArgAt<WeakETag>(1));
                });

            await _exportJobTask.ExecuteAsync(_exportJobRecord, _weakETag, _cancellationToken);

            await _fhirOperationDataStore.DidNotReceiveWithAnyArgs().UpdateExportJobAsync(null, null, CancellationToken.None);
        }

        private SearchResult CreateSearchResult(IEnumerable<ResourceWrapper> resourceWrappers = null, string continuationToken = null)
        {
            if (resourceWrappers == null)
            {
                resourceWrappers = Array.Empty<ResourceWrapper>();
            }

            return new SearchResult(resourceWrappers, new Tuple<string, string>[0], continuationToken);
        }
    }
}
