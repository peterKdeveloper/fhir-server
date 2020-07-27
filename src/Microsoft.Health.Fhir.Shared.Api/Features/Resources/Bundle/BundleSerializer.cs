﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Health.Fhir.Shared.Core.Features.Search;

namespace Microsoft.Health.Fhir.Api.Features.Resources.Bundle
{
    public static class BundleSerializer
    {
        public static async Task Serialize(object input, Stream outputStream)
        {
            var bundle = input as Hl7.Fhir.Model.Bundle;

            using (Utf8JsonWriter writer = new Utf8JsonWriter(outputStream))
            {
                writer.WriteStartObject();

                writer.WriteString("resourceType", bundle.ResourceType.ToString());
                writer.WriteString("id", bundle.Id);
                writer.WriteStartObject("meta");
                writer.WriteString("lastUpdated", bundle.Meta.LastUpdated?.ToString("o"));
                writer.WriteEndObject();

                writer.WriteString("type", bundle.Type?.ToString());

                writer.WriteStartArray("link");

                foreach (var link in bundle.Link)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("relation");
                    writer.WriteStringValue(link.Relation);
                    writer.WritePropertyName("url");
                    writer.WriteStringValue(link.Url);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();

                writer.WriteStartArray("entry");
                foreach (var entry in bundle.Entry)
                {
                    var doc = entry as RawFhirResource;
                    writer.WriteStartObject();
                    writer.WriteString("fullUrl", doc.FullUrl);
                    writer.WritePropertyName("resource");
                    doc.Content.WriteTo(writer);
                    doc.Content.Dispose();

                    writer.WriteStartObject("search");
                    writer.WriteString("mode", doc.Search?.Mode?.ToString());

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();

                writer.WriteEndObject();
                await writer.FlushAsync();
            }
        }
    }
}
