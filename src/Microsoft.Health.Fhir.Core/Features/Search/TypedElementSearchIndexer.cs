﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EnsureThat;
using Hl7.Fhir.ElementModel;
using Hl7.FhirPath;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Features.Search.Converters;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Microsoft.Health.Fhir.Core.Models;
using SearchParamType = Microsoft.Health.Fhir.ValueSets.SearchParamType;

namespace Microsoft.Health.Fhir.Core.Features.Search
{
    /// <summary>
    /// Provides a mechanism to create search indices.
    /// </summary>
    public class TypedElementSearchIndexer : ISearchIndexer
    {
        private readonly ISearchParameterDefinitionManager _searchParameterDefinitionManager;
        private readonly IFhirNodeToSearchValueTypeConverterManager _fhirElementTypeConverterManager;
        private readonly IReferenceToElementResolver _referenceToElementResolver;
        private readonly IModelInfoProvider _modelInfoProvider;
        private readonly ILogger<ISearchIndexer> _logger;
        private readonly ConcurrentDictionary<string, List<string>> _targetTypesLookup = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TypedElementSearchIndexer"/> class.
        /// </summary>
        /// <param name="searchParameterDefinitionManagerResolver">The search parameter definition manager.</param>
        /// <param name="fhirElementTypeConverterManager">The FHIR element type converter manager.</param>
        /// <param name="referenceToElementResolver">Used for parsing reference strings</param>
        /// <param name="modelInfoProvider">Model info provider</param>
        /// <param name="logger">The logger.</param>
        public TypedElementSearchIndexer(
            ISearchParameterDefinitionManager.SupportedSearchParameterDefinitionManagerResolver searchParameterDefinitionManagerResolver,
            IFhirNodeToSearchValueTypeConverterManager fhirElementTypeConverterManager,
            IReferenceToElementResolver referenceToElementResolver,
            IModelInfoProvider modelInfoProvider,
            ILogger<ISearchIndexer> logger)
        {
            EnsureArg.IsNotNull(searchParameterDefinitionManagerResolver, nameof(searchParameterDefinitionManagerResolver));
            EnsureArg.IsNotNull(fhirElementTypeConverterManager, nameof(fhirElementTypeConverterManager));
            EnsureArg.IsNotNull(referenceToElementResolver, nameof(referenceToElementResolver));
            EnsureArg.IsNotNull(modelInfoProvider, nameof(modelInfoProvider));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _searchParameterDefinitionManager = searchParameterDefinitionManagerResolver();
            _fhirElementTypeConverterManager = fhirElementTypeConverterManager;
            _referenceToElementResolver = referenceToElementResolver;
            _modelInfoProvider = modelInfoProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public IReadOnlyCollection<SearchIndexEntry> Extract(ResourceElement resource)
        {
            EnsureArg.IsNotNull(resource, nameof(resource));

            var entries = new List<SearchIndexEntry>();

            var context = _modelInfoProvider.GetEvaluationContext(resource.Instance, _referenceToElementResolver.Resolve);

            IEnumerable<SearchParameterInfo> searchParameters = _searchParameterDefinitionManager.GetSearchParameters(resource.InstanceType);

            foreach (SearchParameterInfo searchParameter in searchParameters)
            {
                if (searchParameter.Name == SearchParameterNames.ResourceType)
                {
                    // We don't index the resource type value. We just use the property on the root document.

                    continue;
                }

                if (searchParameter.Type == SearchParamType.Composite)
                {
                    entries.AddRange(ProcessCompositeSearchParameter(searchParameter, resource.Instance, context));
                }
                else
                {
                    entries.AddRange(ProcessNonCompositeSearchParameter(searchParameter, resource.Instance, context));
                }
            }

            return entries;
        }

        private IEnumerable<SearchIndexEntry> ProcessCompositeSearchParameter(SearchParameterInfo searchParameter, ITypedElement resource, EvaluationContext context)
        {
            Debug.Assert(searchParameter?.Type == SearchParamType.Composite, "The search parameter must be composite.");

            SearchParameterInfo compositeSearchParameterInfo = searchParameter;

            IEnumerable<ITypedElement> rootObjects = resource.Select(searchParameter.Expression, context);

            foreach (var rootObject in rootObjects)
            {
                int numberOfComponents = searchParameter.Component.Count;
                bool skip = false;

                var componentValues = new IReadOnlyList<ISearchValue>[numberOfComponents];

                // For each object extracted from the expression, we will need to evaluate each component.
                for (int i = 0; i < numberOfComponents; i++)
                {
                    SearchParameterComponentInfo component = searchParameter.Component[i];

                    // First find the type of the component.
                    SearchParameterInfo componentSearchParameterDefinition = _searchParameterDefinitionManager.GetSearchParameter(component.DefinitionUrl);

                    IReadOnlyList<ISearchValue> extractedComponentValues = ExtractSearchValues(
                        componentSearchParameterDefinition.Url.ToString(),
                        componentSearchParameterDefinition.Type,
                        componentSearchParameterDefinition.TargetResourceTypes,
                        rootObject,
                        component.Expression,
                        context);

                    // Filter out any search value that's not valid as a composite component.
                    extractedComponentValues = extractedComponentValues
                        .Where(sv => sv.IsValidAsCompositeComponent)
                        .ToArray();

                    if (!extractedComponentValues.Any())
                    {
                        // One of the components didn't have any value and therefore it will not be indexed.
                        skip = true;
                        break;
                    }

                    componentValues[i] = extractedComponentValues;
                }

                if (skip)
                {
                    continue;
                }

                yield return new SearchIndexEntry(compositeSearchParameterInfo, new CompositeSearchValue(componentValues));
            }
        }

        private IEnumerable<SearchIndexEntry> ProcessNonCompositeSearchParameter(SearchParameterInfo searchParameter, ITypedElement resource, EvaluationContext context)
        {
            Debug.Assert(searchParameter?.Type != SearchParamType.Composite, "The search parameter must be non-composite.");

            SearchParameterInfo searchParameterInfo = searchParameter;

            foreach (ISearchValue searchValue in ExtractSearchValues(
                searchParameter.Url.ToString(),
                searchParameter.Type,
                searchParameter.TargetResourceTypes,
                resource,
                searchParameter.Expression,
                context))
            {
                yield return new SearchIndexEntry(searchParameterInfo, searchValue);
            }
        }

        private IReadOnlyList<ISearchValue> ExtractSearchValues(
            string searchParameterDefinitionUrl,
            SearchParamType? searchParameterType,
            IEnumerable<string> allowedReferenceResourceTypes,
            ITypedElement element,
            string fhirPathExpression,
            EvaluationContext context)
        {
            Debug.Assert(searchParameterType != SearchParamType.Composite, "The search parameter must be non-composite.");

            var results = new List<ISearchValue>();

            // For simple value type, we can parse the expression directly.
            IEnumerable<ITypedElement> extractedValues = Enumerable.Empty<ITypedElement>();

            try
            {
                extractedValues = element.Select(fhirPathExpression, context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to extract the values using '{FhirPathExpression}' against '{ElementType}'.",
                    fhirPathExpression,
                    element.GetType());
            }

            Debug.Assert(extractedValues != null, "The extracted values should not be null.");

            // If there is target set, then filter the extracted values to only those types.
            if (searchParameterType == SearchParamType.Reference &&
                allowedReferenceResourceTypes != null &&
                allowedReferenceResourceTypes.Any())
            {
                List<string> targetResourceTypes = _targetTypesLookup.GetOrAdd(searchParameterDefinitionUrl, _ =>
                {
                    return allowedReferenceResourceTypes.Select(t => t.ToString()).ToList();
                });

                // TODO: The expression for reference search parameters in STU3 has issues.
                // The reference search parameter could be pointing to an element that can be multiple types. For example,
                // the Appointment.participant.actor can be type of Patient, Practitioner, Related Person, Location, and so on.
                // Some search parameter could refer to this property but restrict to certain types. For example,
                // Appointment's location search parameter is returned only when Appointment.participant.actor is Location element.
                // The STU3 expressions don't have this restriction so everything is being returned. This is addressed in R4 release (see
                // http://community.fhir.org/t/expression-seems-incorrect-for-reference-search-parameter-thats-only-applicable-to-certain-types/916/2).
                // Therefore, for now, we will need to compare the reference value itself (which can be internal or external references), and restrict
                // the values ourselves.
                extractedValues = extractedValues.Where(ev =>
                {
                    if (ev.InstanceType.Equals("ResourceReference", StringComparison.OrdinalIgnoreCase))
                    {
                        var rr = ev.Scalar("reference") as string;
                        return rr != null && targetResourceTypes.Any(trt => rr.Contains(trt, StringComparison.Ordinal));
                    }

                    return true;
                });
            }

            foreach (var extractedValue in extractedValues)
            {
                if (!_fhirElementTypeConverterManager.TryGetConverter(extractedValue.InstanceType, GetSearchValueTypeForSearchParamType(searchParameterType), out IFhirNodeToSearchValueTypeConverter converter))
                {
                    _logger.LogWarning(
                        "The FHIR element '{ElementType}' is not supported.",
                        extractedValue.InstanceType);

                    continue;
                }

                _logger.LogDebug(
                    "The FHIR element '{ElementType}' will be converted using '{ElementTypeConverter}'.",
                    extractedValue.InstanceType,
                    converter.GetType().FullName);

                results.AddRange(converter.ConvertTo(extractedValue) ?? Enumerable.Empty<ISearchValue>());
            }

            return results;
        }

        internal static Type GetSearchValueTypeForSearchParamType(SearchParamType? searchParamType)
        {
            switch (searchParamType)
            {
                case SearchParamType.Number:
                    return typeof(NumberSearchValue);
                case SearchParamType.Date:
                    return typeof(DateTimeSearchValue);
                case SearchParamType.String:
                    return typeof(StringSearchValue);
                case SearchParamType.Token:
                    return typeof(TokenSearchValue);
                case SearchParamType.Reference:
                    return typeof(ReferenceSearchValue);
                case SearchParamType.Composite:
                    return typeof(CompositeSearchValue);
                case SearchParamType.Quantity:
                    return typeof(QuantitySearchValue);
                case SearchParamType.Uri:
                    return typeof(UriSearchValue);
                case SearchParamType.Special:
                    return typeof(StringSearchValue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(searchParamType), searchParamType, null);
            }
        }
    }
}
