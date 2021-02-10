﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using EnsureThat;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Validation;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Validation;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Shared.Core.Features.Validation
{
    public class ProfileValidator : IProfileValidator
    {
        private readonly IResourceResolver _resolver;

        public ProfileValidator(IProvideProfilesForValidation profilesResolver, IOptions<ValidateOperationConfiguration> options)
        {
            EnsureArg.IsNotNull(profilesResolver, nameof(profilesResolver));
            try
            {
                _resolver = new CachedResolver(
                    new MultiResolver(
                        profilesResolver,
                        ZipSource.CreateValidationSource()),
                    options.Value.CacheDuration);
            }
            catch (Exception)
            {
                // Something went wrong during profile loading, what should we do?
                throw;
            }
        }

        private Validator GetValidator()
        {
            var ctx = new ValidationSettings()
            {
                ResourceResolver = _resolver,
                GenerateSnapshot = true,
                Trace = false,
                ResolveExternalReferences = false,
            };

            var validator = new Validator(ctx);

            return validator;
        }

        public OperationOutcomeIssue[] TryValidate(ITypedElement instance, string profile = null)
        {
            var validator = GetValidator();
            OperationOutcome result;
            if (!string.IsNullOrWhiteSpace(profile))
            {
                result = validator.Validate(instance, profile);
            }
            else
            {
                result = validator.Validate(instance);
            }

            var outcomeIssues = new OperationOutcomeIssue[result.Issue.Count];
            var index = 0;
            foreach (var issue in result.Issue)
            {
                outcomeIssues[index++] = new OperationOutcomeIssue(
                    issue.Severity?.ToString(),
                    issue.Code.ToString(),
                    diagnostics: issue.Diagnostics,
                    detailsText: issue.Details.Text,
                    detailsCodes: new CodableConceptInfo(issue.Details.Coding.Select(x => new Hl7.Fhir.Model.Primitives.Coding(x.System, x.Code, x.Display))),
                    location: issue.Location.ToArray());
            }

            return outcomeIssues;
        }
    }
}
