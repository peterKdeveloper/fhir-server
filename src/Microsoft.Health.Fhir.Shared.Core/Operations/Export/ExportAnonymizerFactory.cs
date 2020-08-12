﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Anonymizer.Core;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Export
{
    public class ExportAnonymizerFactory : IAnonymizerFactory
    {
        private IArtifactProvider _artifactProvider;
        private ILogger<ExportJobTask> _logger;

        public ExportAnonymizerFactory(IArtifactProvider artifactProvider, ILogger<ExportJobTask> logger)
        {
            EnsureArg.IsNotNull(artifactProvider, nameof(artifactProvider));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _artifactProvider = artifactProvider;
            _logger = logger;
        }

        public async Task<IAnonymizer> CreateAnonymizerAsync(string configurationLocation, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNullOrEmpty(configurationLocation, nameof(configurationLocation));

            using (Stream stream = new MemoryStream())
            {
                try
                {
                    await _artifactProvider.FetchAsync(configurationLocation, stream, cancellationToken);
                    stream.Position = 0;
                }
                catch (FileNotFoundException ex)
                {
                    _logger.LogError($"Anonymization configuration file not found: {configurationLocation}");
                    throw new AnonymizationConfigurationNotFoundException(ex.Message, ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to fetch Anonymization configuration file: {configurationLocation}");
                    throw new AnonymizationConfigurationFetchException(ex.Message, ex);
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    string configurationContent = await reader.ReadToEndAsync();
                    try
                    {
                        var engine = new AnonymizerEngine(AnonymizerConfigurationManager.CreateFromSettingsInJson(configurationContent));
                        return new ExportAnonymizer(engine);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to parse configuration file: {ex.Message}");
                        throw new FailedToParseAnonymizationConfigurationException(ex.Message, ex);
                    }
                }
            }
        }
    }
}
