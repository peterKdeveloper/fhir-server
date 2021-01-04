﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using Microsoft.Azure.ContainerRegistry;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Azure.ContainerRegistry;
using Microsoft.Health.Fhir.Azure.ExportDestinationClient;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Operations.ConvertData;
using Microsoft.Health.Fhir.Core.Features.Operations.Export.ExportDestinationClient;
using Microsoft.Health.Fhir.Core.Registration;

namespace Microsoft.Health.Fhir.Azure
{
    public static class FhirServerBuilderAzureRegistrationExtensions
    {
        private const string ExportConfigurationName = "FhirServer:Operations:Export";

        public static IFhirServerBuilder AddAzureExportDestinationClient(this IFhirServerBuilder fhirServerBuilder)
        {
            EnsureArg.IsNotNull(fhirServerBuilder, nameof(fhirServerBuilder));

            fhirServerBuilder.Services.Add<AzureExportDestinationClient>()
                .Transient()
                .AsService<IExportDestinationClient>();

            fhirServerBuilder.Services.AddTransient<ExportDestinationArtifactProvider>();
            fhirServerBuilder.Services.AddTransient<ExportDestinationArtifactAcrProvider>();

            fhirServerBuilder.Services.AddTransient<ArtifactProviderResolver>(serviceProvider => key =>
            {
                switch (key)
                {
                    case "acr":
                        return serviceProvider.GetService<ExportDestinationArtifactAcrProvider>();
                    case "storage":
                        return serviceProvider.GetService<ExportDestinationArtifactProvider>();
                    default:
                        throw null;
                }
            });

            return fhirServerBuilder;
        }

        public static IFhirServerBuilder AddAzureExportClientInitializer(this IFhirServerBuilder fhirServerBuilder, IConfiguration configuration)
        {
            EnsureArg.IsNotNull(fhirServerBuilder, nameof(fhirServerBuilder));
            EnsureArg.IsNotNull(configuration, nameof(configuration));

            var exportJobConfiguration = new ExportJobConfiguration();
            configuration.GetSection(ExportConfigurationName).Bind(exportJobConfiguration);

            if (!string.IsNullOrWhiteSpace(exportJobConfiguration.StorageAccountUri))
            {
                fhirServerBuilder.Services.Add<AzureAccessTokenClientInitializer>()
                    .Transient()
                    .AsService<IExportClientInitializer<CloudBlobClient>>();

                fhirServerBuilder.Services.Add<AzureAccessTokenProvider>()
                    .Transient()
                    .AsService<IAccessTokenProvider>();
            }
            else
            {
                fhirServerBuilder.Services.Add<AzureConnectionStringClientInitializer>()
                    .Transient()
                    .AsService<IExportClientInitializer<CloudBlobClient>>();
            }

            if (!string.IsNullOrWhiteSpace(exportJobConfiguration.AcrServer))
            {
                fhirServerBuilder.Services.Add<AzureContainerRegistryClientInitializer>()
                    .Transient()
                    .AsService<IExportClientInitializer<AzureContainerRegistryClient>>();
            }

            return fhirServerBuilder;
        }

        public static IFhirServerBuilder AddContainerRegistryTokenProvider(this IFhirServerBuilder fhirServerBuilder)
        {
            EnsureArg.IsNotNull(fhirServerBuilder, nameof(fhirServerBuilder));

            fhirServerBuilder.Services.Add<AzureAccessTokenProvider>()
                .Transient()
                .AsService<IAccessTokenProvider>();
            fhirServerBuilder.Services.Add<AzureContainerRegistryAccessTokenProvider>()
                .Singleton()
                .AsService<IContainerRegistryTokenProvider>();

            return fhirServerBuilder;
        }
    }
}
