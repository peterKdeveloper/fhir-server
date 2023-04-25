﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Operations.Import;
using Microsoft.Health.Fhir.SqlServer.Features.Operations.Import.DataGenerator;
using Polly;

namespace Microsoft.Health.Fhir.SqlServer.Features.Operations.Import
{
    internal class SqlResourceBulkImporter : IResourceBulkImporter
    {
        private List<TableBulkCopyDataGenerator> _generators = new List<TableBulkCopyDataGenerator>();
        private ISqlBulkCopyDataWrapperFactory _sqlBulkCopyDataWrapperFactory;
        private ISqlImportOperation _sqlImportOperation;
        private readonly ImportTaskConfiguration _importTaskConfiguration;
        private IImportErrorSerializer _importErrorSerializer;
        private ILogger<SqlResourceBulkImporter> _logger;

        public SqlResourceBulkImporter(
            ISqlImportOperation sqlImportOperation,
            ISqlBulkCopyDataWrapperFactory sqlBulkCopyDataWrapperFactory,
            IImportErrorSerializer importErrorSerializer,
            List<TableBulkCopyDataGenerator> generators,
            IOptions<OperationsConfiguration> operationsConfig,
            ILogger<SqlResourceBulkImporter> logger)
        {
            EnsureArg.IsNotNull(sqlImportOperation, nameof(sqlImportOperation));
            EnsureArg.IsNotNull(sqlBulkCopyDataWrapperFactory, nameof(sqlBulkCopyDataWrapperFactory));
            EnsureArg.IsNotNull(importErrorSerializer, nameof(importErrorSerializer));
            EnsureArg.IsNotNull(generators, nameof(generators));
            EnsureArg.IsNotNull(operationsConfig, nameof(operationsConfig));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _sqlImportOperation = sqlImportOperation;
            _sqlBulkCopyDataWrapperFactory = sqlBulkCopyDataWrapperFactory;
            _importErrorSerializer = importErrorSerializer;
            _generators = generators;
            _importTaskConfiguration = operationsConfig.Value.Import;
            _logger = logger;
        }

        public SqlResourceBulkImporter(
            ISqlImportOperation sqlImportOperation,
            ISqlBulkCopyDataWrapperFactory sqlBulkCopyDataWrapperFactory,
            IImportErrorSerializer importErrorSerializer,
            CompartmentAssignmentTableBulkCopyDataGenerator compartmentAssignmentTableBulkCopyDataGenerator,
            ResourceWriteClaimTableBulkCopyDataGenerator resourceWriteClaimTableBulkCopyDataGenerator,
            DateTimeSearchParamsTableBulkCopyDataGenerator dateTimeSearchParamsTableBulkCopyDataGenerator,
            NumberSearchParamsTableBulkCopyDataGenerator numberSearchParamsTableBulkCopyDataGenerator,
            QuantitySearchParamsTableBulkCopyDataGenerator quantitySearchParamsTableBulkCopyDataGenerator,
            ReferenceSearchParamsTableBulkCopyDataGenerator referenceSearchParamsTableBulkCopyDataGenerator,
            ReferenceTokenCompositeSearchParamsTableBulkCopyDataGenerator referenceTokenCompositeSearchParamsTableBulkCopyDataGenerator,
            StringSearchParamsTableBulkCopyDataGenerator stringSearchParamsTableBulkCopyDataGenerator,
            TokenDateTimeCompositeSearchParamsTableBulkCopyDataGenerator tokenDateTimeCompositeSearchParamsTableBulkCopyDataGenerator,
            TokenNumberNumberCompositeSearchParamsTableBulkCopyDataGenerator tokenNumberNumberCompositeSearchParamsTableBulkCopyDataGenerator,
            TokenQuantityCompositeSearchParamsTableBulkCopyDataGenerator tokenQuantityCompositeSearchParamsTableBulkCopyDataGenerator,
            TokenSearchParamsTableBulkCopyDataGenerator tokenSearchParamsTableBulkCopyDataGenerator,
            TokenStringCompositeSearchParamsTableBulkCopyDataGenerator tokenStringCompositeSearchParamsTableBulkCopyDataGenerator,
            TokenTextSearchParamsTableBulkCopyDataGenerator tokenTextSearchParamsTableBulkCopyDataGenerator,
            TokenTokenCompositeSearchParamsTableBulkCopyDataGenerator tokenTokenCompositeSearchParamsTableBulkCopyDataGenerator,
            UriSearchParamsTableBulkCopyDataGenerator uriSearchParamsTableBulkCopyDataGenerator,
            IOptions<OperationsConfiguration> operationsConfig,
            ILogger<SqlResourceBulkImporter> logger)
        {
            EnsureArg.IsNotNull(sqlImportOperation, nameof(sqlImportOperation));
            EnsureArg.IsNotNull(sqlBulkCopyDataWrapperFactory, nameof(sqlBulkCopyDataWrapperFactory));
            EnsureArg.IsNotNull(importErrorSerializer, nameof(importErrorSerializer));
            EnsureArg.IsNotNull(compartmentAssignmentTableBulkCopyDataGenerator, nameof(compartmentAssignmentTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(resourceWriteClaimTableBulkCopyDataGenerator, nameof(resourceWriteClaimTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(dateTimeSearchParamsTableBulkCopyDataGenerator, nameof(dateTimeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(numberSearchParamsTableBulkCopyDataGenerator, nameof(numberSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(quantitySearchParamsTableBulkCopyDataGenerator, nameof(quantitySearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(referenceSearchParamsTableBulkCopyDataGenerator, nameof(referenceSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(referenceTokenCompositeSearchParamsTableBulkCopyDataGenerator, nameof(referenceTokenCompositeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(stringSearchParamsTableBulkCopyDataGenerator, nameof(stringSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenDateTimeCompositeSearchParamsTableBulkCopyDataGenerator, nameof(tokenDateTimeCompositeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenNumberNumberCompositeSearchParamsTableBulkCopyDataGenerator, nameof(tokenNumberNumberCompositeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenQuantityCompositeSearchParamsTableBulkCopyDataGenerator, nameof(tokenQuantityCompositeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenSearchParamsTableBulkCopyDataGenerator, nameof(tokenSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenStringCompositeSearchParamsTableBulkCopyDataGenerator, nameof(tokenStringCompositeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenTextSearchParamsTableBulkCopyDataGenerator, nameof(tokenTextSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(tokenTokenCompositeSearchParamsTableBulkCopyDataGenerator, nameof(tokenTokenCompositeSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(uriSearchParamsTableBulkCopyDataGenerator, nameof(uriSearchParamsTableBulkCopyDataGenerator));
            EnsureArg.IsNotNull(operationsConfig, nameof(operationsConfig));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _sqlImportOperation = sqlImportOperation;
            _sqlBulkCopyDataWrapperFactory = sqlBulkCopyDataWrapperFactory;
            _importErrorSerializer = importErrorSerializer;

            _generators.Add(compartmentAssignmentTableBulkCopyDataGenerator);
            _generators.Add(resourceWriteClaimTableBulkCopyDataGenerator);
            _generators.Add(dateTimeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(numberSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(quantitySearchParamsTableBulkCopyDataGenerator);
            _generators.Add(referenceSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(referenceTokenCompositeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(stringSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenDateTimeCompositeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenNumberNumberCompositeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenQuantityCompositeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenStringCompositeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenTextSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(tokenTokenCompositeSearchParamsTableBulkCopyDataGenerator);
            _generators.Add(uriSearchParamsTableBulkCopyDataGenerator);

            _importTaskConfiguration = operationsConfig.Value.Import;
            _logger = logger;
        }

        public (Channel<ImportProcessingProgress> progressChannel, Task importTask) Import(Channel<ImportResource> inputChannel, IImportErrorStore importErrorStore, CancellationToken cancellationToken)
        {
            Channel<ImportProcessingProgress> outputChannel = Channel.CreateUnbounded<ImportProcessingProgress>();

            Task importTask = Task.Run(
                async () =>
                {
                    await ImportInternalAsync(inputChannel, outputChannel, importErrorStore, cancellationToken);
                },
                cancellationToken);

            return (outputChannel, importTask);
        }

        public async Task CleanResourceAsync(ImportProcessingJobDefinition inputData, ImportProcessingJobResult result, CancellationToken cancellationToken)
        {
            long beginSequenceId = inputData.BeginSequenceId;
            long endSequenceId = inputData.EndSequenceId;
            long endIndex = result.CurrentIndex;

            if (endSequenceId == 0)
            {
                return;
            }

            try
            {
                await _sqlBulkCopyDataWrapperFactory.EnsureInitializedAsync();
                await _sqlImportOperation.CleanBatchResourceAsync(inputData.ResourceType, beginSequenceId + endIndex, endSequenceId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Failed to clean batch resource.");
                throw;
            }
        }

        private async Task ImportInternalAsync(Channel<ImportResource> inputChannel, Channel<ImportProcessingProgress> outputChannel, IImportErrorStore importErrorStore, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Start to import data to SQL data store.");

                var isMerge = true;

                var checkpointTask = Task.FromResult<ImportProcessingProgress>(null);

                long succeedCount = 0;
                long failedCount = 0;
                long? lastCheckpointIndex = null;
                long currentIndex = -1;
                var resourceParamsBuffer = new Dictionary<string, DataTable>();
                var importErrorBuffer = new List<string>();
                var importTasks = new Queue<Task<ImportProcessingProgress>>();

                List<ImportResource> resourceBuffer = new List<ImportResource>();
                await _sqlBulkCopyDataWrapperFactory.EnsureInitializedAsync();
                await foreach (ImportResource resource in inputChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    if (resource.Id > 0) // this is a temporary hack. it will be removed in stage 2.
                    {
                        isMerge = false;
                    }

                    lastCheckpointIndex = lastCheckpointIndex ?? resource.Index - 1;
                    currentIndex = resource.Index;

                    resourceBuffer.Add(resource);
                    if (resourceBuffer.Count < _importTaskConfiguration.SqlBatchSizeForImportResourceOperation)
                    {
                        continue;
                    }

                    if (isMerge)
                    {
                        ImportResourcesInBuffer(resourceBuffer, importErrorBuffer, cancellationToken, ref succeedCount, ref failedCount);
                        continue;
                    }

                    try
                    {
                        // Handle resources in buffer
                        IEnumerable<ImportResource> resourcesWithError = resourceBuffer.Where(r => r.ContainsError());
                        IEnumerable<SqlBulkCopyDataWrapper> inputResources = resourceBuffer.Where(r => !r.ContainsError()).Select(r => _sqlBulkCopyDataWrapperFactory.CreateSqlBulkCopyDataWrapper(r));
                        IEnumerable<SqlBulkCopyDataWrapper> mergedResources = await _sqlImportOperation.BulkMergeResourceAsync(inputResources, cancellationToken);
                        IEnumerable<SqlBulkCopyDataWrapper> duplicateResourcesNotMerged = inputResources.Except(mergedResources);

                        importErrorBuffer.AddRange(resourcesWithError.Select(r => r.ImportError));
                        await FillResourceParamsBuffer(mergedResources.ToArray(), resourceParamsBuffer);
                        AppendDuplicatedResourceErrorToBuffer(duplicateResourcesNotMerged, importErrorBuffer, 0);

                        succeedCount += mergedResources.Count();
                        failedCount += resourcesWithError.Count() + duplicateResourcesNotMerged.Count();
                    }
                    finally
                    {
                        foreach (ImportResource importResource in resourceBuffer)
                        {
                            var stream = importResource?.CompressedStream;
                            if (stream != null)
                            {
                                await stream.DisposeAsync();
                            }
                        }

                        resourceBuffer.Clear();
                    }
                }

                if (isMerge)
                {
                    ImportResourcesInBuffer(resourceBuffer, importErrorBuffer, cancellationToken, ref succeedCount, ref failedCount);
                }
                else
                {
                    try
                    {
                        // Handle resources in buffer
                        IEnumerable<ImportResource> resourcesWithError = resourceBuffer.Where(r => r.ContainsError());
                        IEnumerable<SqlBulkCopyDataWrapper> inputResources = resourceBuffer.Where(r => !r.ContainsError()).Select(r => _sqlBulkCopyDataWrapperFactory.CreateSqlBulkCopyDataWrapper(r));
                        IEnumerable<SqlBulkCopyDataWrapper> mergedResources = await _sqlImportOperation.BulkMergeResourceAsync(inputResources, cancellationToken);
                        IEnumerable<SqlBulkCopyDataWrapper> duplicateResourcesNotMerged = inputResources.Except(mergedResources);
                        importErrorBuffer.AddRange(resourcesWithError.Select(r => r.ImportError));

                        await FillResourceParamsBuffer(mergedResources.ToArray(), resourceParamsBuffer);

                        AppendDuplicatedResourceErrorToBuffer(duplicateResourcesNotMerged, importErrorBuffer, 0);
                        succeedCount += mergedResources.Count();
                        failedCount += resourcesWithError.Count() + duplicateResourcesNotMerged.Count();
                    }
                    finally
                    {
                        foreach (ImportResource importResource in resourceBuffer)
                        {
                            var stream = importResource?.CompressedStream;
                            if (stream != null)
                            {
                                await stream.DisposeAsync();
                            }
                        }

                        resourceBuffer.Clear();
                    }

                    // Import all remain tables
                    string[] allTablesNotNull = resourceParamsBuffer.Where(r => r.Value.Rows.Count > 0).Select(r => r.Key).ToArray();
                    foreach (string tableName in allTablesNotNull)
                    {
                        DataTable dataTable = resourceParamsBuffer[tableName];
                        await EnqueueTaskAsync(importTasks, () => ImportDataTableAsync(dataTable, cancellationToken), outputChannel);
                    }

                    // Wait all table import task complete
                    while (importTasks.Count > 0)
                    {
                        await importTasks.Dequeue();
                    }
                }

                // Upload remain error logs
                ImportProcessingProgress progress = await UploadImportErrorsAsync(importErrorStore, succeedCount, failedCount, importErrorBuffer.ToArray(), currentIndex, cancellationToken);
                await outputChannel.Writer.WriteAsync(progress, cancellationToken);
            }
            finally
            {
                outputChannel.Writer.Complete();
                _logger.LogInformation("Import data to SQL data store complete.");
            }
        }

        private void ImportResourcesInBuffer(List<ImportResource> resources, List<string> errors, CancellationToken cancellationToken, ref long succeedCount, ref long failedCount)
        {
            var resourcesWithError = resources.Where(r => r.ContainsError());
            var resourcesWithoutError = resources.Where(r => !r.ContainsError()).ToList();
            var resourcesDedupped = resourcesWithoutError.GroupBy(_ => _.Resource.ToResourceKey()).Select(_ => _.First()).ToList();
            var mergedResources = _sqlImportOperation.MergeResourcesAsync(resourcesDedupped, cancellationToken).Result;
            var dupsNotMerged = resourcesWithoutError.Except(resourcesDedupped);

            errors.AddRange(resourcesWithError.Select(r => r.ImportError));
            AppendDuplicateErrorsToBuffer(dupsNotMerged, errors);

            succeedCount += mergedResources.Count();
            failedCount += resourcesWithError.Count() + dupsNotMerged.Count();

            resources.Clear();
        }

        private async Task FillResourceParamsBuffer(SqlBulkCopyDataWrapper[] mergedResources, Dictionary<string, DataTable> resourceParamsBuffer)
        {
            List<Task> runningTasks = new List<Task>();

            foreach (TableBulkCopyDataGenerator generator in _generators)
            {
                if (!resourceParamsBuffer.ContainsKey(generator.TableName))
                {
                    resourceParamsBuffer[generator.TableName] = generator.GenerateDataTable();
                }

                while (runningTasks.Count >= _importTaskConfiguration.SqlMaxDatatableProcessConcurrentCount)
                {
                    Task completeTask = await Task.WhenAny(runningTasks);
                    await completeTask;

                    runningTasks.Remove(completeTask);
                }

                DataTable table = resourceParamsBuffer[generator.TableName];

                runningTasks.Add(Task.Run(() =>
                {
                    foreach (SqlBulkCopyDataWrapper resourceWrapper in mergedResources)
                    {
                        generator.FillDataTable(table, resourceWrapper);
                    }
                }));
            }

            while (runningTasks.Count > 0)
            {
                Task completeTask = await Task.WhenAny(runningTasks);
                await completeTask;

                runningTasks.Remove(completeTask);
            }
        }

        private void AppendDuplicateErrorsToBuffer(IEnumerable<ImportResource> resources, List<string> importErrorBuffer)
        {
            foreach (var resource in resources)
            {
                importErrorBuffer.Add(_importErrorSerializer.Serialize(resource.Index, string.Format(Resources.FailedToImportForDuplicatedResource, resource.Resource.ResourceId, resource.Index), resource.Offset));
            }
        }

        private void AppendDuplicatedResourceErrorToBuffer(IEnumerable<SqlBulkCopyDataWrapper> resources, List<string> importErrorBuffer, long offset)
        {
            foreach (SqlBulkCopyDataWrapper resourceWrapper in resources)
            {
                importErrorBuffer.Add(_importErrorSerializer.Serialize(resourceWrapper.Index, string.Format(Resources.FailedToImportForDuplicatedResource, resourceWrapper.Resource.ResourceId, resourceWrapper.Index), offset));
            }
        }

        private async Task<ImportProcessingProgress> UploadImportErrorsAsync(IImportErrorStore importErrorStore, long succeedCount, long failedCount, string[] importErrors, long lastIndex, CancellationToken cancellationToken)
        {
            try
            {
                await importErrorStore.UploadErrorsAsync(importErrors, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Failed to upload error logs.");
                throw;
            }

            ImportProcessingProgress progress = new ImportProcessingProgress();
            progress.SucceedImportCount = succeedCount;
            progress.FailedImportCount = failedCount;
            progress.CurrentIndex = lastIndex + 1;

            // Return progress for checkpoint progress
            return progress;
        }

        private async Task<ImportProcessingProgress> ImportDataTableAsync(DataTable table, CancellationToken cancellationToken)
        {
            try
            {
                await Policy.Handle<SqlException>()
                    .WaitAndRetryAsync(
                        retryCount: 10,
                        sleepDurationProvider: (retryCount) => TimeSpan.FromSeconds(5 * (retryCount - 1)))
                    .ExecuteAsync(async () =>
                    {
                        await _sqlImportOperation.BulkCopyDataAsync(table, cancellationToken);
                    });

                // Return null for non checkpoint progress
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Failed to import table: {Table}", table.TableName);

                throw;
            }
        }

        private async Task<Task<ImportProcessingProgress>> EnqueueTaskAsync(Queue<Task<ImportProcessingProgress>> importTasks, Func<Task<ImportProcessingProgress>> newTaskFactory, Channel<ImportProcessingProgress> progressChannel)
        {
            while (importTasks.Count >= _importTaskConfiguration.SqlMaxImportOperationConcurrentCount)
            {
                ImportProcessingProgress progress = await importTasks.Dequeue();
                if (progress != null)
                {
                    await progressChannel.Writer.WriteAsync(progress);
                }
            }

            Task<ImportProcessingProgress> newTask = newTaskFactory();
            importTasks.Enqueue(newTask);

            return newTask;
        }
    }
}
