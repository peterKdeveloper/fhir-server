﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Fhir.Tests.E2E.Common;
using Microsoft.Health.Fhir.Web;
using Microsoft.Net.Http.Headers;
using Xunit;
using HttpMethod = System.Net.Http.HttpMethod;

namespace Microsoft.Health.Fhir.Tests.E2E.Rest
{
    public class CorsTests : IClassFixture<HttpIntegrationTestFixture<Startup>>
    {
        private readonly FhirClient _client;

        public CorsTests(HttpIntegrationTestFixture<Startup> fixture)
        {
            _client = fixture.FhirClient;
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenGettingOptions_GivenAppropriateHeaders_TheServerShouldReturnTheAppropriateCorsHeaders()
        {
            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Options,
            };

            message.Headers.Add(HeaderNames.Origin, "http://local");
            message.Headers.Add(HeaderNames.AccessControlRequestMethod, "PUT");
            message.Headers.Add(HeaderNames.AccessControlRequestMethod, "POST");
            message.Headers.Add(HeaderNames.AccessControlRequestHeaders, "authorization");
            message.Headers.Add(HeaderNames.AccessControlRequestHeaders, "content-type");
            message.RequestUri = new Uri(_client.HttpClient.BaseAddress, "/patient");

            HttpResponseMessage response = await _client.HttpClient.SendAsync(message);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Contains("*", response.Headers.GetValues(HeaderNames.AccessControlAllowOrigin));

            Assert.Contains("\"PUT,POST\"", response.Headers.GetValues(HeaderNames.AccessControlAllowMethods));

            Assert.Contains("authorization,content-type", response.Headers.GetValues(HeaderNames.AccessControlAllowHeaders));

            Assert.Equal("1440", response.Headers.GetValues(HeaderNames.AccessControlMaxAge).First());
        }
    }
}
