﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Subscriptions.Channels;
using Microsoft.Health.Fhir.Subscriptions.Persistence;

namespace Microsoft.Health.Fhir.Subscriptions.HeartBeats
{
    public class HeartBeatBackgroundService : BackgroundService
    {
        private readonly ILogger<HeartBeatBackgroundService> _logger;
        private readonly IScopeProvider<SubscriptionManager> _subscriptionManager;
        private readonly StorageChannelFactory _storageChannelFactory;

        public HeartBeatBackgroundService(ILogger<HeartBeatBackgroundService> logger, IScopeProvider<SubscriptionManager> subscriptionManager, StorageChannelFactory storageChannelFactory)
        {
            _logger = logger;
            _subscriptionManager = subscriptionManager;
            _storageChannelFactory = storageChannelFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            var nextHeartBeat = new HashSet<string>();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await periodicTimer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    // our logic
                    using IScoped<SubscriptionManager> subscriptionManager = _subscriptionManager.Invoke();
                    await subscriptionManager.Value.SyncSubscriptionsAsync(stoppingToken);

                    var activeSubscriptions = await subscriptionManager.Value.GetActiveSubscriptionsAsync(stoppingToken);
                    var subscriptionsWithHeartbeat = activeSubscriptions.Where(subscription => !subscription.Channel.HeartBeatPeriod.Equals(null));

                    // go through subscriptions with heartbeat, if not in hashset then send heartbeat and add to hashset, if not check the corresponding value in the hashset and get time

                    // if time has expired then send heartbeat and set new heartbeat time, else skip

                    // if send heartbeat fails then mark as error and send back
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Error executing timer");
                }
            }
        }
    }
}
