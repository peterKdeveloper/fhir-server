﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Health.Client;
using Microsoft.Health.Fhir.Tests.E2E.Common;
using Polly;
using Polly.Retry;
using Task = System.Threading.Tasks.Task;
#if !R5
using RestfulCapabilityMode = Hl7.Fhir.Model.CapabilityStatement.RestfulCapabilityMode;
#endif

namespace Microsoft.Health.Fhir.Tests.E2E.Rest
{
    /// <summary>
    /// Represents a FHIR server for end-to-end testing.
    /// Creates and caches <see cref="TestFhirClient"/> instances that target the server.
    /// </summary>
    public abstract class TestFhirServer : IDisposable
    {
        private readonly ConcurrentDictionary<(ResourceFormat format, TestApplication clientApplication, TestUser user), Lazy<TestFhirClient>> _cache = new ConcurrentDictionary<(ResourceFormat format, TestApplication clientApplication, TestUser user), Lazy<TestFhirClient>>();
        private readonly AsyncLocal<SessionTokenContainer> _asyncLocalSessionTokenContainer = new AsyncLocal<SessionTokenContainer>();

        private Dictionary<string, AuthenticationHttpMessageHandler> _authenticationHandlers = new Dictionary<string, AuthenticationHttpMessageHandler>();

        protected TestFhirServer(Uri baseAddress)
        {
            EnsureArg.IsNotNull(baseAddress, nameof(baseAddress));

            BaseAddress = baseAddress;
        }

        protected internal bool SecurityEnabled { get; set; }

        protected internal Uri TokenUri { get; set; }

        protected internal Uri AuthorizeUri { get; set; }

        public Uri BaseAddress { get; }

        public TestFhirClient GetTestFhirClient(ResourceFormat format, bool reusable = true, DelegatingHandler authenticationHandler = null)
        {
            return GetTestFhirClient(format, TestApplications.GlobalAdminServicePrincipal, null, reusable, authenticationHandler);
        }

        public TestFhirClient GetTestFhirClient(ResourceFormat format, TestApplication clientApplication, TestUser user, bool reusable = true, DelegatingHandler authenticationHandler = null)
        {
            if (_asyncLocalSessionTokenContainer.Value == null)
            {
                // Ensure that we are able to preserve session tokens across requests in this execution context and its children.
                _asyncLocalSessionTokenContainer.Value = new SessionTokenContainer();
            }

            if (!reusable)
            {
                return CreateFhirClient(format, clientApplication, user, authenticationHandler);
            }

            return _cache.GetOrAdd(
                    (format, clientApplication, user),
                    (tuple, fhirServer) =>
                        new Lazy<TestFhirClient>(() => CreateFhirClient(tuple.format, tuple.clientApplication, tuple.user, authenticationHandler)),
                    this)
                .Value;
        }

        private TestFhirClient CreateFhirClient(ResourceFormat format, TestApplication clientApplication, TestUser user, DelegatingHandler authenticationHandler = null)
        {
            ConfigureSecurityOptions().GetAwaiter().GetResult();

            var sessionMessageHandler = new SessionMessageHandler(CreateMessageHandler(), _asyncLocalSessionTokenContainer);

            if (authenticationHandler != null)
            {
                authenticationHandler.InnerHandler = CreateMessageHandler();
                sessionMessageHandler.InnerHandler = authenticationHandler;
            }
            else if (SecurityEnabled && !clientApplication.Equals(TestApplications.InvalidClient))
            {
                string authKey = GenerateKey();
                if (_authenticationHandlers.ContainsKey(authKey))
                {
                    sessionMessageHandler.InnerHandler = _authenticationHandlers[authKey];
                }
                else
                {
                    AuthenticationHttpMessageHandler authHandler;

                    var authHttpClient = new HttpClient(new SessionMessageHandler(CreateMessageHandler(), _asyncLocalSessionTokenContainer)) { BaseAddress = BaseAddress };
                    if (user == null)
                    {
                        string scope = clientApplication.Equals(TestApplications.WrongAudienceClient) ? clientApplication.ClientId : AuthenticationSettings.Scope;
                        string resource = clientApplication.Equals(TestApplications.WrongAudienceClient) ? clientApplication.ClientId : AuthenticationSettings.Resource;

                        var credentialConfiguration = new OAuth2ClientCredentialConfiguration(
                            TokenUri,
                            resource,
                            scope,
                            clientApplication.ClientId,
                            clientApplication.ClientSecret);
                        var credentialProvider = new OAuth2ClientCredentialProvider(Options.Create(credentialConfiguration), authHttpClient);
                        authHandler = new AuthenticationHttpMessageHandler(credentialProvider);
                    }
                    else
                    {
                        var credentialConfiguration = new OAuth2UserPasswordCredentialConfiguration(
                            TokenUri,
                            AuthenticationSettings.Resource,
                            AuthenticationSettings.Scope,
                            clientApplication.ClientId,
                            clientApplication.ClientSecret,
                            user.UserId,
                            user.Password);
                        var credentialProvider = new OAuth2UserPasswordCredentialProvider(Options.Create(credentialConfiguration), authHttpClient);
                        authHandler = new AuthenticationHttpMessageHandler(credentialProvider);
                    }

                    _authenticationHandlers.Add(authKey, authHandler);
                    authHandler.InnerHandler = CreateMessageHandler();
                    sessionMessageHandler.InnerHandler = authHandler;
                }
            }

            var httpClient = new HttpClient(sessionMessageHandler) { BaseAddress = BaseAddress };

            return new TestFhirClient(httpClient, this, format, clientApplication, user);

            string GenerateKey()
            {
                return $"{clientApplication.ClientId}:{user?.UserId}";
            }
        }

        protected abstract HttpMessageHandler CreateMessageHandler();

        /// <summary>
        /// Set the security options on the <see cref="Hl7.Fhir.Rest.FhirClient"/>.
        /// <remarks>Examines the metadata endpoint to determine if there's a token and authorize url exposed and sets the property <see cref="SecurityEnabled"/> to <value>true</value> or <value>false</value> based on this.
        /// Additionally, the <see cref="TokenUri"/> is set if it is are found.</remarks>
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        internal async Task ConfigureSecurityOptions(CancellationToken cancellationToken = default)
        {
            bool localSecurityEnabled = false;

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, "metadata"));
            var httpClient = new HttpClient(new SessionMessageHandler(CreateMessageHandler(), _asyncLocalSessionTokenContainer))
            {
                BaseAddress = BaseAddress,
            };
            HttpResponseMessage response = await httpClient.SendAsync(requestMessage, cancellationToken);

            string content = await response.Content.ReadAsStringAsync();
            CapabilityStatement metadata = new FhirJsonParser().Parse<CapabilityStatement>(content);

            foreach (var rest in metadata.Rest.Where(r => r.Mode == RestfulCapabilityMode.Server))
            {
                var oauth = rest.Security?.GetExtension(Core.Features.Security.Constants.SmartOAuthUriExtension);
                if (oauth != null)
                {
                    var tokenUrl = oauth.GetExtensionValue<FhirUri>(Core.Features.Security.Constants.SmartOAuthUriExtensionToken).Value;
                    var authorizeUrl = oauth.GetExtensionValue<FhirUri>(Core.Features.Security.Constants.SmartOAuthUriExtensionAuthorize).Value;

                    localSecurityEnabled = true;
                    TokenUri = new Uri(tokenUrl);
                    AuthorizeUri = new Uri(authorizeUrl);

                    break;
                }
            }

            SecurityEnabled = localSecurityEnabled;
        }

        public virtual void Dispose()
        {
            foreach (Lazy<TestFhirClient> cacheValue in _cache.Values)
            {
                if (cacheValue.IsValueCreated)
                {
                    cacheValue.Value.HttpClient.Dispose();
                }
            }
        }

        /// <summary>
        /// An <see cref="HttpMessageHandler"/> that maintains Cosmos DB session consistency between requests.
        /// </summary>
        private class SessionMessageHandler : DelegatingHandler
        {
            private readonly AsyncLocal<SessionTokenContainer> _asyncLocalSessionTokenContainer;
            private readonly AsyncRetryPolicy _polly;

            public SessionMessageHandler(HttpMessageHandler innerHandler, AsyncLocal<SessionTokenContainer> asyncLocalSessionTokenContainer)
                : base(innerHandler)
            {
                _asyncLocalSessionTokenContainer = asyncLocalSessionTokenContainer;
                EnsureArg.IsNotNull(asyncLocalSessionTokenContainer, nameof(asyncLocalSessionTokenContainer));
                _polly = Policy.Handle<HttpRequestException>(x =>
                {
                    if (x.InnerException is IOException || x.InnerException is SocketException)
                    {
                        return true;
                    }

                    return false;
                }).WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                SessionTokenContainer sessionTokenContainer = _asyncLocalSessionTokenContainer.Value;
                if (sessionTokenContainer == null)
                {
                    throw new InvalidOperationException($"{nameof(SessionTokenContainer)} has not been set for the execution context");
                }

                string latestValue = sessionTokenContainer.SessionToken;

                if (!string.IsNullOrEmpty(latestValue))
                {
                    request.Headers.TryAddWithoutValidation("x-ms-session-token", latestValue);
                }

                request.Headers.TryAddWithoutValidation("x-ms-consistency-level", "Session");

                if (request.Content != null)
                {
                    await request.Content.LoadIntoBufferAsync();
                }

                HttpResponseMessage response = await _polly.ExecuteAsync(async () => await base.SendAsync(request, cancellationToken));

                if (response.Headers.TryGetValues("x-ms-session-token", out var tokens))
                {
                    sessionTokenContainer.SessionToken = tokens.SingleOrDefault();
                }

                return response;
            }
        }

        private class SessionTokenContainer
        {
            public string SessionToken { get; set; }
        }
    }
}
