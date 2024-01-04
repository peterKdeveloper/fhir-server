﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Messages.Delete;
using Microsoft.Health.JobManagement;
using Newtonsoft.Json;

namespace Microsoft.Health.Fhir.Core.Features.Operations.BulkDelete
{
    internal class BulkDeleteDefinition : IJobData, IThrottleableJobRecord
    {
        public BulkDeleteDefinition(
            JobType jobType,
            DeleteOperation deleteOperation,
            string type,
            IList<Tuple<string, string>> searchParameters,
            string url,
            string baseUrl,
            string parentRequestId,
            long startingResourceCount = 0,
            ResourceVersionType versionType = ResourceVersionType.Latest)
        {
            TypeId = (int)jobType;
            DeleteOperation = deleteOperation;
            Type = type;
            SearchParameters = searchParameters;
            Url = url;
            BaseUrl = baseUrl;
            ParentRequestId = parentRequestId;
            ExpectedResourceCount = startingResourceCount;
            VersionType = versionType;

            MaximumNumberOfResourcesPerQuery = 1000;
            QueryDelayIntervalInMilliseconds = 5;
            TargetDataStoreUsagePercentage = 90;
        }

        [JsonConstructor]
        protected BulkDeleteDefinition()
        {
        }

        [JsonProperty(JobRecordProperties.TypeId)]
        public int TypeId { get; set; }

        [JsonProperty(JobRecordProperties.DeleteOperation)]
        public DeleteOperation DeleteOperation { get; private set; }

        [JsonProperty(JobRecordProperties.Type)]
        public string Type { get; private set; }

        [JsonProperty(JobRecordProperties.SearchParameters)]
        public IList<Tuple<string, string>> SearchParameters { get; private set; }

        [JsonProperty(JobRecordProperties.Url)]
        public string Url { get; private set; }

        [JsonProperty(JobRecordProperties.BaseUrl)]
        public string BaseUrl { get; private set; }

        [JsonProperty(JobRecordProperties.ParentRequestId)]
        public string ParentRequestId { get; private set; }

        [JsonProperty(JobRecordProperties.ExpectedResourceCount)]
        public long ExpectedResourceCount { get; private set; }

        [JsonProperty(JobRecordProperties.VersionType)]
        public ResourceVersionType VersionType { get; private set; }

        [JsonProperty(JobRecordProperties.MaximumNumberOfResourcesPerQuery)]
        public uint MaximumNumberOfResourcesPerQuery { get; set; }

        /// <summary>
        /// Controls the time between queries of resources to be reindexed
        /// </summary>
        [JsonProperty(JobRecordProperties.QueryDelayIntervalInMilliseconds)]
        public int QueryDelayIntervalInMilliseconds { get; set; }

        /// <summary>
        /// Controls the target percentage of how much of the allocated
        /// data store resources to use
        /// Ex: 1 - 100 percent of provisioned datastore resources
        /// 0 means the value is not set, no throttling will occur
        /// </summary>
        [JsonProperty(JobRecordProperties.TargetDataStoreUsagePercentage)]
        public ushort? TargetDataStoreUsagePercentage { get; set; }
    }
}
