﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Health.Fhir.Core.Features.Context
{
    public interface IFhirContext
    {
        string CorrelationId { get; }

        Coding RequestType { get; set; }

        Coding RequestSubType { get; set; }

        string HttpMethod { get; set; }

        Uri RequestUri { get; set; }

        string RouteName { get; set; }

        ClaimsPrincipal Principal { get; set; }

        IDictionary<string, StringValues> RequestHeaders { get; }

        IDictionary<string, StringValues> ResponseHeaders { get; }
    }
}
