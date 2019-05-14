﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Core.Features.Search.Converters;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Search.Converters
{
    public abstract class FhirElementToSearchValueTypeConverterTests<TTypeConverter, TElement>
        where TTypeConverter : IFhirElementToSearchValueTypeConverter, new()
        where TElement : Element, new()
    {
        protected TTypeConverter TypeConverter { get; } = new TTypeConverter();

        protected TElement Element { get; } = new TElement();

        protected abstract SearchParamType DefaultSearchParamType { get; }

        [Fact]
        public void GivenANullValue_WhenConverted_ThenNoSearchValueShouldBeCreated()
        {
            IEnumerable<ISearchValue> values = TypeConverter.ConvertTo(null, DefaultSearchParamType);

            Assert.NotNull(values);
            Assert.Empty(values);
        }

        protected void Test<TValue>(Action<TElement> setup, Action<TValue, ISearchValue> validator, params TValue[] expected)
        {
            Test(setup, validator, DefaultSearchParamType, expected);
        }

        protected void Test<TValue>(Action<TElement> setup, Action<TValue, ISearchValue> validator, SearchParamType searchParamType, params TValue[] expected)
        {
            setup(Element);

            IEnumerable<ISearchValue> values = TypeConverter.ConvertTo(Element, searchParamType);

            Assert.NotNull(values);
            Assert.Collection(
                values,
                expected.Select(e => new Action<ISearchValue>(sv => validator(e, sv))).ToArray());
        }

        protected void Test(Action<TElement> setup, SearchParamType? searchParamType = null)
        {
            setup(Element);

            IEnumerable<ISearchValue> values = TypeConverter.ConvertTo(Element, searchParamType ?? DefaultSearchParamType);

            Assert.NotNull(values);
            Assert.Empty(values);
        }
    }
}
