﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Tests.Common
{
    public static class Deserializers
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private static readonly FhirJsonParser JsonParser = new(new ParserSettings() { PermissiveParsing = true, TruncateDateTimeToDate = true });
#pragma warning restore CS0618 // Type or member is obsolete

        public static ResourceDeserializer ResourceDeserializer => new((FhirResourceFormat.Json, ConvertJson));

        private static ResourceElement ConvertJson(RawResource rawResource, string version, DateTimeOffset? lastModified)
        {
            var resource = JsonParser.Parse<Resource>(rawResource.Data);

            if (!rawResource.IsMetaSet)
            {
                resource.VersionId = version;
            }

            if (lastModified.HasValue && lastModified != DateTimeOffset.MinValue)
            {
                resource.Meta.LastUpdated = lastModified;
            }

            return resource.ToTypedElement().ToResourceElement();
        }
    }
}
