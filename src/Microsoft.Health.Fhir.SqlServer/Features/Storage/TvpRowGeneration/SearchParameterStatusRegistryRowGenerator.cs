﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Microsoft.Health.Fhir.Core.Features.Search.Registry;
using Microsoft.Health.Fhir.SqlServer.Features.Schema.Model;
using Microsoft.Health.SqlServer.Features.Schema.Model;

namespace Microsoft.Health.Fhir.SqlServer.Features.Storage.TvpRowGeneration
{
    internal class SearchParameterStatusRegistryRowGenerator : ITableValuedParameterRowGenerator<List<ResourceSearchParameterStatus>, VLatest.SearchParamTableTypeRow>
    {
        public IEnumerable<VLatest.SearchParamTableTypeRow> GenerateRows(List<ResourceSearchParameterStatus> searchParameterStatuses)
        {
            return searchParameterStatuses.Select(searchParameterStatus => new VLatest.SearchParamTableTypeRow(
                    searchParameterStatus.Uri.ToString(),
                    searchParameterStatus.Status.ToString(),
                    searchParameterStatus.IsPartiallySupported))
                .ToList();
        }
    }
}
