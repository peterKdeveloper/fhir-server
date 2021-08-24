﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.SqlServer.Features.Schema;
using Microsoft.Health.TaskManagement;

namespace Microsoft.Health.Fhir.Api.Features.BackgroundTaskService
{
    /// <summary>
    /// The background service used to host the <see cref="TaskHosting"/>.
    /// </summary>
    public class TaskHostingBackgroundService : BackgroundService
    {
        private readonly Func<IScoped<TaskHosting>> _taskHostingFactory;
        private readonly TaskHostingConfiguration _taskHostingConfiguration;
        private readonly ILogger<TaskHostingBackgroundService> _logger;
        private readonly SchemaInformation _schemaInformation;

        public TaskHostingBackgroundService(
            Func<IScoped<TaskHosting>> taskHostingFactory,
            IOptions<TaskHostingConfiguration> taskHostingConfiguration,
            SchemaInformation schemaInformation,
            ILogger<TaskHostingBackgroundService> logger)
        {
            EnsureArg.IsNotNull(taskHostingFactory, nameof(taskHostingFactory));
            EnsureArg.IsNotNull(taskHostingConfiguration?.Value, nameof(taskHostingConfiguration));
            EnsureArg.IsNotNull(schemaInformation, nameof(schemaInformation));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _taskHostingFactory = taskHostingFactory;
            _taskHostingConfiguration = taskHostingConfiguration.Value;
            _schemaInformation = schemaInformation;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TaskHostingBackgroundService begin.");

            try
            {
                using (IScoped<TaskHosting> taskHosting = _taskHostingFactory())
                {
                    var taskHostingValue = taskHosting.Value;
                    if (_taskHostingConfiguration != null)
                    {
                        taskHostingValue.PollingFrequencyInSeconds = _taskHostingConfiguration.PollingFrequencyInSeconds ?? taskHostingValue.PollingFrequencyInSeconds;
                        taskHostingValue.MaxRunningTaskCount = _taskHostingConfiguration.MaxRunningTaskCount ?? taskHostingValue.MaxRunningTaskCount;
                        taskHostingValue.TaskHeartbeatIntervalInSeconds = _taskHostingConfiguration.TaskHeartbeatIntervalInSeconds ?? taskHostingValue.TaskHeartbeatIntervalInSeconds;
                        taskHostingValue.TaskHeartbeatTimeoutThresholdInSeconds = _taskHostingConfiguration.TaskHeartbeatTimeoutThresholdInSeconds ?? taskHostingValue.TaskHeartbeatTimeoutThresholdInSeconds;
                    }

                    using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    await taskHostingValue.StartAsync(_schemaInformation, cancellationTokenSource);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TaskHostingBackgroundService crash.");
            }
            finally
            {
                _logger.LogInformation("TaskHostingBackgroundService end.");
            }
        }
    }
}
