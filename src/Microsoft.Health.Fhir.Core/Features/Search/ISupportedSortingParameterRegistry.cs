﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Search
{
    public interface ISupportedSortingParameterRegistry
    {
        bool ValidateSortings(IReadOnlyList<(SearchParameterInfo searchParameter, SortOrder sortOrder)> sortings, out IReadOnlyList<string> errorMessages);
    }
}
