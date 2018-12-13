﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Health.Fhir.Core.Exceptions;

namespace Microsoft.Health.Fhir.Api.Features.Formatters
{
    public static class FormatterExtensions
    {
        private static readonly IDictionary<ResourceFormat, string> ResourceFormatContentType = new Dictionary<ResourceFormat, string>
        {
            { ResourceFormat.Json, ContentType.JSON_CONTENT_HEADER },
            { ResourceFormat.Xml, ContentType.XML_CONTENT_HEADER },
        };

        internal static string GetClosestClientMediaType(this IEnumerable<TextOutputFormatter> outputFormatters, ResourceFormat resourceFormat, IEnumerable<string> acceptHeaders)
        {
            // When overriding the MediaType with the query string parameter
            // some browsers don't display the response when returning "application/fhir+xml".
            // For this reason we try to match a media type from the OutputFormatter with the request Accept header.

            string closestClientMediaType = null;
            string preferred = resourceFormat.ToContentType();

            if (outputFormatters != null)
            {
                // Gets formatters that can write the desired format
                var validFormatters = outputFormatters
                    .Where(x => x.GetSupportedContentTypes(preferred, typeof(Resource)) != null)
                    .ToArray();

                var acceptHeadersArray = acceptHeaders.ToArray();

                // Using the valid formatters, select the correct content type header for the client
                closestClientMediaType = acceptHeadersArray
                    .SelectMany(x => validFormatters.SelectMany(y => y.GetSupportedContentTypes(x, typeof(Resource)) ?? Enumerable.Empty<string>()))
                    .Distinct()
                    .Intersect(acceptHeadersArray)
                    .FirstOrDefault();
            }

            return closestClientMediaType ?? preferred;
        }

        private static string ToContentType(this ResourceFormat resourceType)
        {
            if (ResourceFormatContentType.TryGetValue(resourceType, out string contentType))
            {
                return contentType;
            }

            throw new UnsupportedMediaTypeException(Resources.UnsupportedFormatParameter);
        }
    }
}
