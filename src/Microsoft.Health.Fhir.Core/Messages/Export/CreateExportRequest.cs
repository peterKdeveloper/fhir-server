﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using EnsureThat;
using MediatR;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Messages.Export
{
    public class CreateExportRequest : IRequest<CreateExportResponse>
    {
        public CreateExportRequest(Uri requestUri, string resourceType = null, PartialDateTime since = null, string anonymizationConfigurationLocation = null, string anonymizationConfigurationFileHash = null)
        {
            EnsureArg.IsNotNull(requestUri, nameof(requestUri));

            RequestUri = requestUri;
            ResourceType = resourceType;
            Since = since;
            AnonymizationConfigurationLocation = anonymizationConfigurationLocation;
            AnonymizationConfigurationFileHash = anonymizationConfigurationFileHash;
        }

        public Uri RequestUri { get; }

        public string ResourceType { get; }

        public PartialDateTime Since { get; }

        public string AnonymizationConfigurationLocation { get; }

        public string AnonymizationConfigurationFileHash { get; }
    }
}
