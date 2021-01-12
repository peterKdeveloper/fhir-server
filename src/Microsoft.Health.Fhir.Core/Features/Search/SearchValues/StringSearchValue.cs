﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using EnsureThat;

namespace Microsoft.Health.Fhir.Core.Features.Search.SearchValues
{
    /// <summary>
    /// Represents a string search value.
    /// </summary>
    public class StringSearchValue : ISearchValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StringSearchValue"/> class.
        /// </summary>
        /// <param name="s">The string value.</param>
        public StringSearchValue(string s)
        {
            EnsureArg.IsNotNullOrWhiteSpace(s, nameof(s));

            String = s.UnescapeSearchParameterValue();
        }

        /// <summary>
        /// Gets the unescaped string value.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Represents a FHIR search parameter of type string")]
        public string String { get; }

        /// <inheritdoc />
        public bool IsValidAsCompositeComponent => true;

        /// <inheritdoc />
        public bool IsMin { get; set; }

        /// <inheritdoc />
        public bool IsMax { get; set; }

        /// <summary>
        /// Parses the string value to an instance of <see cref="StringSearchValue"/>.
        /// </summary>
        /// <param name="s">The string to be parsed.</param>
        /// <returns>An instance of <see cref="StringSearchValue"/>.</returns>
        public static StringSearchValue Parse(string s)
        {
            EnsureArg.IsNotNullOrWhiteSpace(s, nameof(s));

            return new StringSearchValue(s);
        }

        /// <inheritdoc />
        public void AcceptVisitor(ISearchValueVisitor visitor)
        {
            EnsureArg.IsNotNull(visitor, nameof(visitor));

            visitor.Visit(this);
        }

        /// <inheritdoc />
        public int Compare(ISearchValue searchValue)
        {
            if (searchValue == null)
            {
                throw new ArgumentException("Value to be compared to cannot be null");
            }

            var otherValue = searchValue as StringSearchValue;
            if (otherValue == null)
            {
                throw new ArgumentException($"Value to be compared should be of type {typeof(StringSearchValue)}");
            }

            return string.Compare(ToString(), otherValue.ToString(), StringComparison.CurrentCultureIgnoreCase);
        }

        public bool Equals([AllowNull] ISearchValue other)
        {
            if (other == null)
            {
                return false;
            }

            var stringSearchValueOther = other as StringSearchValue;

            if (stringSearchValueOther == null)
            {
                return false;
            }

            return String.Equals(stringSearchValueOther.String, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return String.EscapeSearchParameterValue();
        }
    }
}
