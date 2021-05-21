﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.TaskManagement;
using Newtonsoft.Json;
using TaskStatus = Microsoft.Health.TaskManagement.TaskStatus;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Import
{
    public class ImportOrchestratorTask : ITask
    {
        public const short ImportOrchestratorTaskId = 2;

        private const int DefaultPollingFrequencyInSeconds = 3;
        private const long DefaultResourceSizePerByte = 64;

        private ImportOrchestratorTaskInputData _orchestratorInputData;
        private RequestContextAccessor<IFhirRequestContext> _contextAccessor;
        private ImportOrchestratorTaskContext _orchestratorTaskContext; // TODO : changed later
        private ITaskManager _taskManager;
        private ISequenceIdGenerator<long> _sequenceIdGenerator;
        private IFhirDataBulkImportOperation _fhirDataBulkImportOperation;
        private IContextUpdater _contextUpdater;
        private ILogger<ImportOrchestratorTask> _logger;
        private IIntegrationDataStoreClient _integrationDataStoreClient;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private List<(Uri resourceUri, TaskInfo taskInfo)> _runningTasks = new List<(Uri resourceUri, TaskInfo taskInfo)>();

        public ImportOrchestratorTask(
            ImportOrchestratorTaskInputData orchestratorInputData,
            ImportOrchestratorTaskContext orchestratorTaskContext,
            ITaskManager taskManager,
            ISequenceIdGenerator<long> sequenceIdGenerator,
            IContextUpdater contextUpdater,
            RequestContextAccessor<IFhirRequestContext> contextAccessor,
            IFhirDataBulkImportOperation fhirDataBulkImportOperation,
            IIntegrationDataStoreClient integrationDataStoreClient,
            ILoggerFactory loggerFactory)
        {
            EnsureArg.IsNotNull(orchestratorInputData, nameof(orchestratorInputData));
            EnsureArg.IsNotNull(orchestratorTaskContext, nameof(orchestratorTaskContext));
            EnsureArg.IsNotNull(taskManager, nameof(taskManager));
            EnsureArg.IsNotNull(sequenceIdGenerator, nameof(sequenceIdGenerator));
            EnsureArg.IsNotNull(contextUpdater, nameof(contextUpdater));
            EnsureArg.IsNotNull(contextAccessor, nameof(contextAccessor));
            EnsureArg.IsNotNull(fhirDataBulkImportOperation, nameof(fhirDataBulkImportOperation));
            EnsureArg.IsNotNull(integrationDataStoreClient, nameof(integrationDataStoreClient));
            EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));

            _orchestratorInputData = orchestratorInputData;
            _orchestratorTaskContext = orchestratorTaskContext;
            _taskManager = taskManager;
            _sequenceIdGenerator = sequenceIdGenerator;
            _contextUpdater = contextUpdater;
            _contextAccessor = contextAccessor;
            _fhirDataBulkImportOperation = fhirDataBulkImportOperation;
            _integrationDataStoreClient = integrationDataStoreClient;
            _logger = loggerFactory.CreateLogger<ImportOrchestratorTask>();
        }

        public string RunId { get; set; }

        public int PollingFrequencyInSeconds { get; set; } = DefaultPollingFrequencyInSeconds;

        public async Task<TaskResultData> ExecuteAsync()
        {
            var fhirRequestContext = new FhirRequestContext(
                    method: "Import",
                    uriString: _orchestratorInputData.RequestUri.ToString(),
                    baseUriString: _orchestratorInputData.BaseUri.ToString(),
                    correlationId: _orchestratorInputData.TaskId,
                    requestHeaders: new Dictionary<string, StringValues>(),
                    responseHeaders: new Dictionary<string, StringValues>())
            {
                IsBackgroundTask = true,
            };

            _contextAccessor.RequestContext = fhirRequestContext;

            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            ImportTaskResult result = null;
            try
            {
                if (_orchestratorTaskContext.Progress == ImportOrchestratorTaskProgress.Initalized)
                {
                    await ValidateResourcesAsync(cancellationToken);

                    _orchestratorTaskContext.Progress = ImportOrchestratorTaskProgress.InputResourcesValidated;
                    await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);

                    _logger.LogInformation("Input Resources Validated");
                }

                if (_orchestratorTaskContext.Progress == ImportOrchestratorTaskProgress.InputResourcesValidated)
                {
                    await _fhirDataBulkImportOperation.PreprocessAsync(cancellationToken);

                    _orchestratorTaskContext.Progress = ImportOrchestratorTaskProgress.PreprocessCompleted;
                    await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);

                    _logger.LogInformation("Preprocess Completed");
                }

                if (_orchestratorTaskContext.Progress == ImportOrchestratorTaskProgress.PreprocessCompleted)
                {
                    _orchestratorTaskContext.DataProcessingTasks = await GenerateSubTaskRecordsAsync(cancellationToken);
                    _orchestratorTaskContext.Progress = ImportOrchestratorTaskProgress.SubTaskRecordsGenerated;
                    await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);

                    _logger.LogInformation("SubTask Records Generated");
                }

                if (_orchestratorTaskContext.Progress == ImportOrchestratorTaskProgress.SubTaskRecordsGenerated)
                {
                    result = await ExecuteDataProcessingTasksAsync(cancellationToken);

                    _orchestratorTaskContext.Progress = ImportOrchestratorTaskProgress.SubTasksCompleted;
                    await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);

                    _logger.LogInformation("SubTasks Completed");
                }

                if (_orchestratorTaskContext.Progress == ImportOrchestratorTaskProgress.SubTasksCompleted)
                {
                    await _fhirDataBulkImportOperation.DeleteDuplicatedResourcesAsync(cancellationToken);
                    await _fhirDataBulkImportOperation.PostprocessAsync(cancellationToken);

                    _orchestratorTaskContext.Progress = ImportOrchestratorTaskProgress.PostprocessCompleted;
                    await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);

                    _logger.LogInformation("Postprocess Completed");
                }
            }
            catch (TaskCanceledException taskCanceledEx)
            {
                _logger.LogWarning(taskCanceledEx, "Import task canceled. {0}", taskCanceledEx.Message);

                await TryToCancelRunningTasksAsync();
                return new TaskResultData(TaskResult.Canceled, taskCanceledEx.Message);
            }
            catch (OperationCanceledException canceledEx)
            {
                _logger.LogWarning(canceledEx, "Import task canceled. {0}", canceledEx.Message);

                await TryToCancelRunningTasksAsync();
                return new TaskResultData(TaskResult.Canceled, canceledEx.Message);
            }
            catch (IntegrationDataStoreException integrationDataStoreEx)
            {
                _logger.LogError(integrationDataStoreEx, "Failed to access input files.");

                ImportTaskErrorResult errorResult = new ImportTaskErrorResult();
                errorResult.HttpStatusCode = integrationDataStoreEx.StatusCode;
                errorResult.ErrorMessage = integrationDataStoreEx.Message;

                return new TaskResultData(TaskResult.Fail, JsonConvert.SerializeObject(errorResult));
            }
            catch (ImportFileEtagNotMatchException eTagEx)
            {
                _logger.LogError(eTagEx, "Import file etag not match.");

                ImportTaskErrorResult errorResult = new ImportTaskErrorResult();
                errorResult.HttpStatusCode = HttpStatusCode.BadRequest;
                errorResult.ErrorMessage = eTagEx.Message;

                return new TaskResultData(TaskResult.Fail, JsonConvert.SerializeObject(errorResult));
            }
            catch (ImportProcessingException processingEx)
            {
                _logger.LogError(processingEx, "Failed to process input resources.");

                ImportTaskErrorResult errorResult = new ImportTaskErrorResult();
                errorResult.HttpStatusCode = HttpStatusCode.BadRequest;
                errorResult.ErrorMessage = processingEx.Message;

                return new TaskResultData(TaskResult.Fail, JsonConvert.SerializeObject(errorResult));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import data.");

                ImportTaskErrorResult errorResult = new ImportTaskErrorResult();
                errorResult.HttpStatusCode = HttpStatusCode.InternalServerError;
                errorResult.ErrorMessage = ex.Message;

                throw new RetriableTaskException(JsonConvert.SerializeObject(errorResult));
            }

            result.TransactionTime = _orchestratorInputData.TaskCreateTime;
            return new TaskResultData(TaskResult.Success, JsonConvert.SerializeObject(result));
        }

        private async Task ValidateResourcesAsync(CancellationToken cancellationToken)
        {
            foreach (var input in _orchestratorInputData.Input)
            {
                Dictionary<string, object> properties = await _integrationDataStoreClient.GetPropertiesAsync(input.Url, cancellationToken);
                if (!string.IsNullOrEmpty(input.Etag))
                {
                    if (!input.Etag.Equals(properties[IntegrationDataStoreClientConstants.BlobPropertyETag]))
                    {
                        throw new ImportFileEtagNotMatchException(string.Format("Input file Etag not match. {0}", input.Url));
                    }
                }
            }
        }

        private async Task UpdateProgressAsync(ImportOrchestratorTaskContext context, CancellationToken cancellationToken)
        {
            await _contextUpdater.UpdateContextAsync(JsonConvert.SerializeObject(context), cancellationToken);
        }

        private async Task<Dictionary<Uri, TaskInfo>> GenerateSubTaskRecordsAsync(CancellationToken cancellationToken)
        {
            Dictionary<Uri, TaskInfo> result = new Dictionary<Uri, TaskInfo>();

            long beginSequenceId = _sequenceIdGenerator.GetCurrentSequenceId();

            foreach (var input in _orchestratorInputData.Input)
            {
                string taskId = Guid.NewGuid().ToString("N");

                Dictionary<string, object> properties = await _integrationDataStoreClient.GetPropertiesAsync(input.Url, cancellationToken);
                long blobSizeInBytes = (long)properties[IntegrationDataStoreClientConstants.BlobPropertyLength];
                long estimatedResourceNumber = CalculateResourceNumberByResourceSize(blobSizeInBytes, DefaultResourceSizePerByte);
                long endSequenceId = beginSequenceId + estimatedResourceNumber;

                ImportProcessingTaskInputData importTaskPayload = new ImportProcessingTaskInputData()
                {
                    ResourceLocation = input.Url.ToString(),
                    UriString = _orchestratorInputData.RequestUri.ToString(),
                    BaseUriString = _orchestratorInputData.BaseUri.ToString(),
                    ResourceType = input.Type,
                    TaskId = taskId,
                    BeginSequenceId = beginSequenceId,
                    EndSequenceId = endSequenceId,
                };

                TaskInfo processingTask = new TaskInfo()
                {
                    QueueId = _orchestratorInputData.ProcessingTaskQueueId,
                    TaskId = taskId,
                    TaskTypeId = ImportProcessingTask.ImportProcessingTaskId,
                    InputData = JsonConvert.SerializeObject(importTaskPayload),
                    MaxRetryCount = _orchestratorInputData.ProcessingTaskMaxRetryCount,
                };

                result[input.Url] = processingTask;

                beginSequenceId = endSequenceId;
            }

            return result;
        }

        private static long CalculateResourceNumberByResourceSize(long blobSizeInBytes, long resourceSizePerBytes)
        {
            return Math.Max((blobSizeInBytes / resourceSizePerBytes) + 1, 10000L);
        }

        private async Task<ImportTaskResult> ExecuteDataProcessingTasksAsync(CancellationToken cancellationToken)
        {
            List<ImportOperationOutcome> completedOperationOutcome = new List<ImportOperationOutcome>();
            List<ImportFailedOperationOutcome> failedOperationOutcome = new List<ImportFailedOperationOutcome>();

            foreach ((Uri resourceUri, TaskInfo taskInfo) in _orchestratorTaskContext.DataProcessingTasks.ToArray())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                while (_runningTasks.Count >= _orchestratorInputData.MaxConcurrentProcessingTaskCount)
                {
                    List<Uri> completedTaskResourceUris = await MonitorRunningTasksAsync(_runningTasks, cancellationToken);

                    if (completedTaskResourceUris.Count > 0)
                    {
                        AddToResult(completedOperationOutcome, failedOperationOutcome, completedTaskResourceUris);

                        _runningTasks.RemoveAll(t => completedTaskResourceUris.Contains(t.resourceUri));
                        await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(PollingFrequencyInSeconds), cancellationToken);
                    }
                }

                TaskInfo taskInfoFromServer = await _taskManager.GetTaskAsync(taskInfo.TaskId, cancellationToken);
                if (taskInfoFromServer == null)
                {
                    taskInfoFromServer = await _taskManager.CreateTaskAsync(taskInfo, false, cancellationToken);
                }

                _orchestratorTaskContext.DataProcessingTasks[resourceUri] = taskInfoFromServer;
                if (taskInfoFromServer.Status != TaskStatus.Completed)
                {
                    _runningTasks.Add((resourceUri, taskInfoFromServer));
                    await Task.Delay(TimeSpan.FromSeconds(PollingFrequencyInSeconds), cancellationToken);
                }
                else
                {
                    AddToResult(completedOperationOutcome, failedOperationOutcome, new List<Uri>() { resourceUri });
                }
            }

            while (_runningTasks.Count > 0)
            {
                List<Uri> completedTaskResourceUris = await MonitorRunningTasksAsync(_runningTasks, cancellationToken);

                if (completedTaskResourceUris.Count > 0)
                {
                    AddToResult(completedOperationOutcome, failedOperationOutcome, completedTaskResourceUris);

                    _runningTasks.RemoveAll(t => completedTaskResourceUris.Contains(t.resourceUri));
                    await UpdateProgressAsync(_orchestratorTaskContext, cancellationToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(PollingFrequencyInSeconds), cancellationToken);
                }
            }

            return new ImportTaskResult()
            {
                Request = _orchestratorInputData.RequestUri.ToString(),
                Output = completedOperationOutcome,
                Error = failedOperationOutcome,
            };
        }

        private void AddToResult(List<ImportOperationOutcome> completedOperationOutcome, List<ImportFailedOperationOutcome> failedOperationOutcome, List<Uri> completedTaskResourceUris)
        {
            foreach (Uri completedResourceUri in completedTaskResourceUris)
            {
                TaskInfo completeTaskInfo = _orchestratorTaskContext.DataProcessingTasks[completedResourceUri];
                TaskResultData taskResultData = JsonConvert.DeserializeObject<TaskResultData>(completeTaskInfo.Result);

                if (taskResultData.Result == TaskResult.Success)
                {
                    ImportProcessingTaskResult procesingTaskResult = JsonConvert.DeserializeObject<ImportProcessingTaskResult>(taskResultData.ResultData);
                    completedOperationOutcome.Add(new ImportOperationOutcome() { Type = procesingTaskResult.ResourceType, Count = procesingTaskResult.SucceedCount, InputUrl = completedResourceUri });
                    if (procesingTaskResult.FailedCount > 0)
                    {
                        failedOperationOutcome.Add(new ImportFailedOperationOutcome() { Type = procesingTaskResult.ResourceType, Count = procesingTaskResult.FailedCount, InputUrl = completedResourceUri, Url = procesingTaskResult.ErrorLogLocation });
                    }
                }
                else if (taskResultData.Result == TaskResult.Fail)
                {
                    throw new ImportProcessingException(string.Format("Failed to process file: {0}. {1}", completedResourceUri, taskResultData));
                }
                else if (taskResultData.Result == TaskResult.Canceled)
                {
                    throw new OperationCanceledException(taskResultData.ResultData);
                }
            }
        }

        private async Task<List<Uri>> MonitorRunningTasksAsync(List<(Uri resourceUri, TaskInfo taskInfo)> runningTasks, CancellationToken cancellationToken)
        {
            List<Uri> completedTaskResourceUris = new List<Uri>();

            foreach ((Uri runningResourceUri, TaskInfo runningTaskInfo) in runningTasks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                TaskInfo latestTaskInfo = await _taskManager.GetTaskAsync(runningTaskInfo.TaskId, cancellationToken);

                _orchestratorTaskContext.DataProcessingTasks[runningResourceUri] = latestTaskInfo;
                if (latestTaskInfo.Status == TaskStatus.Completed)
                {
                    completedTaskResourceUris.Add(runningResourceUri);
                }
            }

            return completedTaskResourceUris;
        }

        public async Task TryToCancelRunningTasksAsync()
        {
            List<string> runningTaskIds = new List<string>();

            foreach ((_, TaskInfo runningTaskInfo) in _runningTasks)
            {
                try
                {
                    runningTaskIds.Add(runningTaskInfo.TaskId);
                    await _taskManager.CancelTaskAsync(runningTaskInfo.TaskId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "failed to cancel task {0}", runningTaskInfo.TaskId);
                }
            }

            while (true)
            {
                if (runningTaskIds.Count == 0)
                {
                    break;
                }

                string[] currentRunningTaskIds = runningTaskIds.ToArray();

                foreach (string runningTaskId in currentRunningTaskIds)
                {
                    try
                    {
                        TaskInfo taskInfo = await _taskManager.GetTaskAsync(runningTaskId, CancellationToken.None);
                        if (taskInfo.Status != TaskStatus.Running)
                        {
                            runningTaskIds.Remove(runningTaskId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get task info for canceled task {0}", runningTaskId);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            try
            {
                await _fhirDataBulkImportOperation.DeleteDuplicatedResourcesAsync(CancellationToken.None);
                await _fhirDataBulkImportOperation.PostprocessAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean resource after import operation cancelled.");
            }
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public bool IsCancelling()
        {
            return _cancellationTokenSource?.IsCancellationRequested ?? false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }
}
