﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Extensions.Logging;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Import
{
    public class ImportResourceLoader : IImportResourceLoader
    {
        private const int DefaultChannelMaxCapacity = 500;
        private const int DefaultMaxBatchSize = 1000;

        private IIntegrationDataStoreClient _integrationDataStoreClient;
        private IImportResourceParser _importResourceParser;
        private IImportErrorSerializer _importErrorSerializer;
        private ILogger<ImportResourceLoader> _logger;

        public ImportResourceLoader(
            IIntegrationDataStoreClient integrationDataStoreClient,
            IImportResourceParser importResourceParser,
            IImportErrorSerializer importErrorSerializer,
            ILogger<ImportResourceLoader> logger)
        {
            _integrationDataStoreClient = EnsureArg.IsNotNull(integrationDataStoreClient, nameof(integrationDataStoreClient));
            _importResourceParser = EnsureArg.IsNotNull(importResourceParser, nameof(importResourceParser));
            _importErrorSerializer = EnsureArg.IsNotNull(importErrorSerializer, nameof(importErrorSerializer));
            _logger = EnsureArg.IsNotNull(logger, nameof(logger));
        }

        public int MaxBatchSize { get; set; } = DefaultMaxBatchSize;

        public int ChannelMaxCapacity { get; set; } = DefaultChannelMaxCapacity;

        public (Channel<ImportResource> resourceChannel, Task loadTask) LoadResources(string resourceLocation, long offset, int bytesToRead, string resourceType, ImportMode importMode, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotEmptyOrWhiteSpace(resourceLocation, nameof(resourceLocation));

            var outputChannel = Channel.CreateBounded<ImportResource>(ChannelMaxCapacity);

            var loadTask = Task.Run(async () => await LoadResourcesInternalAsync(outputChannel, resourceLocation, offset, bytesToRead, resourceType, importMode, cancellationToken), cancellationToken);

            return (outputChannel, loadTask);
        }

        private async Task LoadResourcesInternalAsync(Channel<ImportResource> outputChannel, string resourceLocation, long offset, int bytesToRead, string resourceType, ImportMode importMode, CancellationToken cancellationToken)
        {
            string leaseId = null;

            try
            {
                _logger.LogInformation("Start to load resource from store.");

                // Try to acquire lease to block change on the blob.
                leaseId = await _integrationDataStoreClient.TryAcquireLeaseAsync(new Uri(resourceLocation), Guid.NewGuid().ToString("N"), cancellationToken);

                using var stream = _integrationDataStoreClient.DownloadResource(new Uri(resourceLocation), offset, cancellationToken);
                using var reader = new StreamReader(stream);

                long currentIndex = 0;
                long currentBytesRead = 0;
                var buffer = new List<(string line, long index, int length)>();

                var skipFirstLine = true;
                while ((currentBytesRead <= bytesToRead) && !reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (line, endOfLineLength) = await ReadLine(reader);

                    var length = Encoding.UTF8.GetByteCount(line) + endOfLineLength;
                    currentBytesRead += length;

                    if (offset > 0 && skipFirstLine) // skip first line but make sure that its length is counted above to avoid processing same records twice
                    {
                        skipFirstLine = false;
                        continue;
                    }

                    currentIndex++;

                    buffer.Add((line, currentIndex, length));

                    if (buffer.Count < MaxBatchSize)
                    {
                        continue;
                    }

                    foreach (var importResource in await ParseImportRawContentAsync(resourceType, buffer, offset, importMode))
                    {
                        await outputChannel.Writer.WriteAsync(importResource, cancellationToken);
                    }
                }

                foreach (var importResource in await ParseImportRawContentAsync(resourceType, buffer, offset, importMode))
                {
                    await outputChannel.Writer.WriteAsync(importResource, cancellationToken);
                }

                _logger.LogInformation("{CurrentIndex} lines loaded.", currentIndex);
            }
            finally
            {
                outputChannel.Writer.Complete();

                if (!string.IsNullOrEmpty(leaseId))
                {
                    await _integrationDataStoreClient.TryReleaseLeaseAsync(new Uri(resourceLocation), leaseId, cancellationToken);
                }

                _logger.LogInformation("Load resource from store complete.");
            }
        }

        // This hadles both \n and \r\n line ends. It does not work with \r.
        private static async Task<(string line, int endOfLineLength)> ReadLine(StreamReader reader)
        {
            var endOfLineLength = 0;
            var line = new StringBuilder();
            var buffer = new char[1];
            while (await reader.ReadAsync(buffer, 0, 1) > 0)
            {
                var currentChar = buffer[0];
                if (currentChar == '\n')
                {
                    endOfLineLength = 1;
                    break;
                }
                else if (currentChar == '\r')
                {
                    var nextChar = (char)reader.Peek();
                    if (nextChar == '\n')
                    {
                        endOfLineLength = 2;
                        await reader.ReadAsync(buffer, 0, 1);
                        break;
                    }
                }

                line.Append(currentChar);
            }

            return (line.ToString(), endOfLineLength); // output line is never null
        }

        private async Task<IEnumerable<ImportResource>> ParseImportRawContentAsync(string resourceType, List<(string content, long index, int length)> rawContents, long offset, ImportMode importMode)
        {
            return await Task.Run(() =>
            {
                var results = new List<ImportResource>();

                foreach ((string content, long index, int length) in rawContents)
                {
                    try
                    {
                        ImportResource importResource = _importResourceParser.Parse(index, offset, length, content, importMode);

                        if (!string.IsNullOrEmpty(resourceType) && !resourceType.Equals(importResource.ResourceWrapper?.ResourceTypeName, StringComparison.Ordinal))
                        {
                            throw new FormatException("Resource type not match.");
                        }

                        results.Add(importResource);
                    }
                    catch (Exception ex)
                    {
                        // May contains customer's data, no error logs here.
                        results.Add(new ImportResource(index, offset, _importErrorSerializer.Serialize(index, ex, offset)));
                    }
                }

                rawContents.Clear();

                return results;
            });
        }
    }
}
