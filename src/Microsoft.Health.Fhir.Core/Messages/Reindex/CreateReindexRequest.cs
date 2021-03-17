﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using MediatR;

namespace Microsoft.Health.Fhir.Core.Messages.Reindex
{
    public class CreateReindexRequest : IRequest<CreateReindexResponse>
    {
        public CreateReindexRequest(
            ushort? maximumConcurrency = null,
            uint? maximumResourcesPerQuery = null,
            int? queryDelayIntervalInMilliseconds = null,
            ushort? targetDataStoreResourcePercentage = null)
        {
            MaximumConcurrency = maximumConcurrency;
            MaximumResourcesPerQuery = maximumResourcesPerQuery;
            QueryDelayIntervalInMilliseconds = queryDelayIntervalInMilliseconds;
            TargetDataStoreResourcePercentage = targetDataStoreResourcePercentage;
        }

        public ushort? MaximumConcurrency { get; }

        public uint? MaximumResourcesPerQuery { get; }

        public int? QueryDelayIntervalInMilliseconds { get; }

        public ushort? TargetDataStoreResourcePercentage { get; }
    }
}
