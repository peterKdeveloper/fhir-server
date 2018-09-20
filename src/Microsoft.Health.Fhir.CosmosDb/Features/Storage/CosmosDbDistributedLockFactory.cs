﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using EnsureThat;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Extensions.DependencyInjection;

namespace Microsoft.Health.Fhir.CosmosDb.Features.Storage
{
    public class CosmosDbDistributedLockFactory : ICosmosDbDistributedLockFactory
    {
        private readonly Func<IScoped<IDocumentClient>> _provider;
        private readonly ILogger<CosmosDbDistributedLock> _logger;

        public CosmosDbDistributedLockFactory(Func<IScoped<IDocumentClient>> provider, ILogger<CosmosDbDistributedLock> logger)
        {
            EnsureArg.IsNotNull(provider, nameof(provider));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _provider = provider;
            _logger = logger;
        }

        public ICosmosDbDistributedLock Create(Uri collectionUri, string lockId)
        {
            EnsureArg.IsNotNull(collectionUri, nameof(collectionUri));
            EnsureArg.IsNotNullOrEmpty(lockId, nameof(lockId));

            return new CosmosDbDistributedLock(_provider, collectionUri, lockId, _logger);
        }

        public ICosmosDbDistributedLock Create(IDocumentClient client, Uri collectionUri, string lockId)
        {
            EnsureArg.IsNotNull(collectionUri, nameof(collectionUri));
            EnsureArg.IsNotNull(collectionUri, nameof(collectionUri));
            EnsureArg.IsNotNullOrEmpty(lockId, nameof(lockId));

            return new CosmosDbDistributedLock(() => new DocumentClientScope(client), collectionUri, lockId, _logger);
        }
    }
}
