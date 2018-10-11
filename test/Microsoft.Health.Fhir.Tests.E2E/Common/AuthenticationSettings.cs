﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using Microsoft.Health.Fhir.Web;

namespace Microsoft.Health.Fhir.Tests.E2E.Common
{
    public static class AuthenticationSettings
    {
        public static string Scope => GetEnvironmentVariableWithDefault("Scope", DevelopmentIdentityProviderConfiguration.Audience);

        public static string Resource => GetEnvironmentVariableWithDefault("Resource", DevelopmentIdentityProviderConfiguration.Audience);

        private static string GetEnvironmentVariableWithDefault(string environmentVariableName, string defaultValue)
        {
            var environmentVariable = Environment.GetEnvironmentVariable(environmentVariableName);

            return string.IsNullOrWhiteSpace(environmentVariable) ? defaultValue : environmentVariable;
        }
    }
}
