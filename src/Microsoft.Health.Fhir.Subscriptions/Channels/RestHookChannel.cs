﻿// -------------------------------------------------------------------------------------------------
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
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Subscriptions.Models;
using Microsoft.Health.Fhir.Subscriptions.Persistence;

namespace Microsoft.Health.Fhir.Subscriptions.Channels
{
    [ChannelType(SubscriptionChannelType.RestHook)]
    public class RestHookChannel : IRestHookChannel
    {
        private HttpClient _httpClient;
        private ILogger _logger;

        public RestHookChannel(ILogger logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public Task PublishAsync(IReadOnlyCollection<ResourceWrapper> resources, ChannelInfo channelInfo, DateTimeOffset transactionTime, CancellationToken cancellationToken)
        {
            var paramater = new Parameters();
            throw new NotImplementedException();

            // IRestHookChannel with additional methods for handshake, heartbeat, payload for subscription notification
        }

        public async Task SendPayload(
        ChannelInfo chanelInfo,
        string contents)
        {
            HttpRequestMessage request = null!;

            // send the request to the endpoint
            try
            {
                // build our request
                request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(chanelInfo.Endpoint),
                    Content = new StringContent(contents, Encoding.UTF8, chanelInfo.ContentType.ToString()),
                };

                // send our request
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // check the status code
                if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                    (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    // failure
                   _logger.LogError($"REST POST to {chanelInfo.Endpoint} failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"REST POST {chanelInfo.ChannelType} to {chanelInfo.Endpoint} failed: {ex.Message}");
            }
            finally
            {
                if (request != null)
                {
                    request.Dispose();
                }
            }
        }
    }
}
