﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Microsoft.Health.Fhir.SqlServer.Features.Schema.Model;
using Microsoft.Health.Fhir.SqlServer.Features.Storage.TvpRowGeneration.Merge;

namespace Microsoft.Health.Fhir.SqlServer.Features.Storage.TvpRowGeneration
{
    internal class TokenDateTimeCompositeSearchParamListRowGenerator : CompositeSearchParamRowGenerator<(TokenSearchValue component1, DateTimeSearchValue component2), TokenDateTimeCompositeSearchParamListRow>
    {
        private readonly TokenSearchParamListRowGenerator _tokenRowGenerator;
        private readonly DateTimeSearchParamListRowGenerator _dateTimeRowGenerator;

        public TokenDateTimeCompositeSearchParamListRowGenerator(
            SqlServerFhirModel model,
            TokenSearchParamListRowGenerator tokenRowGenerator,
            DateTimeSearchParamListRowGenerator dateTimeV1RowGenerator,
            SearchParameterToSearchValueTypeMap searchParameterTypeMap)
            : base(model, searchParameterTypeMap)
        {
            _tokenRowGenerator = tokenRowGenerator;
            _dateTimeRowGenerator = dateTimeV1RowGenerator;
        }

        internal override bool TryGenerateRow(
            short resourceTypeId,
            long resourceSurrogateId,
            short searchParamId,
            (TokenSearchValue component1, DateTimeSearchValue component2) searchValue,
            HashSet<TokenDateTimeCompositeSearchParamListRow> results,
            out TokenDateTimeCompositeSearchParamListRow row)
        {
            if (_tokenRowGenerator.TryGenerateRow(resourceTypeId, resourceSurrogateId, default, searchValue.component1, null, out var token1Row) &&
                _dateTimeRowGenerator.TryGenerateRow(resourceTypeId, resourceSurrogateId, default, searchValue.component2, null, out var token2Row))
            {
                row = new TokenDateTimeCompositeSearchParamListRow(
                    resourceTypeId,
                    resourceSurrogateId,
                    searchParamId,
                    token1Row.SystemId,
                    token1Row.Code,
                    token1Row.CodeOverflow,
                    token2Row.StartDateTime,
                    token2Row.EndDateTime,
                    token2Row.IsLongerThanADay);

                return results == null || results.Add(row);
            }

            row = default;
            return false;
        }

        internal IEnumerable<string> GenerateCSVs(IReadOnlyList<MergeResourceWrapper> resources)
        {
            foreach (var row in GenerateRows(resources))
            {
                yield return $"{row.ResourceTypeId},{row.ResourceSurrogateId},{row.SearchParamId},{row.SystemId1},{row.Code1},{row.StartDateTime2},{row.EndDateTime2},{row.IsLongerThanADay2}";
            }
        }
    }
}
