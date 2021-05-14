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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Operations.Import;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Operations.Import
{
    public class ImportResourceLoaderTests
    {
        [Fact]
        public async Task GivenResourceLoader_WhenLoadResources_AllResoruceShouldBeLoad()
        {
            await VerifyResourceLoaderAsync(1234, 21, 0);
        }

        [Fact]
        public async Task GivenResourceLoader_WhenLoadResourcesFromMiddle_AllResoruceShouldBeLoad()
        {
            await VerifyResourceLoaderAsync(1234, 21, 20);
        }

        [Fact]
        public async Task GivenResourceLoader_WhenLoadResourcesCountEqualsBatchSize_AllResoruceShouldBeLoad()
        {
            await VerifyResourceLoaderAsync(21, 21, 0);
        }

        [Fact]
        public async Task GivenResourceLoader_WhenLoadResourcesCountLessThanBatchSize_AllResoruceShouldBeLoad()
        {
            await VerifyResourceLoaderAsync(1, 21, 0);
        }

        [Fact]
        public async Task GivenResourceLoader_WhenLoadResourcesWithParseException_AllResoruceShouldBeLoadAndErroShouldBeReturned()
        {
            string errorMessage = "error";
            using MemoryStream stream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(stream);
            await writer.WriteLineAsync("test");
            await writer.FlushAsync();

            stream.Position = 0;

            IIntegrationDataStoreClient integrationDataStoreClient = Substitute.For<IIntegrationDataStoreClient>();
            integrationDataStoreClient.DownloadResource(Arg.Any<Uri>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(stream);

            IImportResourceParser importResourceParser = Substitute.For<IImportResourceParser>();
            importResourceParser.Parse(Arg.Any<string>())
                .Returns(callInfo =>
                {
                    throw new InvalidOperationException(errorMessage);
                });

            IImportErrorSerializer serializer = Substitute.For<IImportErrorSerializer>();
            serializer.Serialize(Arg.Any<long>(), Arg.Any<Exception>())
                .Returns(callInfo =>
                {
                    Exception ex = (Exception)callInfo[1];
                    return ex.Message;
                });

            Func<long, long> idGenerator = (i) => i;
            ImportResourceLoader loader = new ImportResourceLoader(integrationDataStoreClient, importResourceParser, serializer, NullLogger<ImportResourceLoader>.Instance);

            (Channel<ImportResource> outputChannel, Task importTask) = loader.LoadResources("http://dummy", 0, null, idGenerator, CancellationToken.None);

            int errorCount = 0;
            await foreach (ImportResource resource in outputChannel.Reader.ReadAllAsync())
            {
                Assert.Equal(errorMessage, resource.ImportError);
                ++errorCount;
            }

            await importTask;

            Assert.Equal(1, errorCount);
        }

        [Fact]
        public async Task GivenResourceLoader_WhenCancelLoadTask_DataLoadTaskShouldBeCanceled()
        {
            string errorMessage = "error";
            using MemoryStream stream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(stream);
            await writer.WriteLineAsync("test");
            await writer.WriteLineAsync("test");
            await writer.WriteLineAsync("test");
            await writer.WriteLineAsync("test");
            await writer.FlushAsync();

            stream.Position = 0;

            AutoResetEvent resetEvent1 = new AutoResetEvent(false);
            ManualResetEvent resetEvent2 = new ManualResetEvent(false);

            IIntegrationDataStoreClient integrationDataStoreClient = Substitute.For<IIntegrationDataStoreClient>();
            integrationDataStoreClient.DownloadResource(Arg.Any<Uri>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(stream);

            IImportResourceParser importResourceParser = Substitute.For<IImportResourceParser>();
            importResourceParser.Parse(Arg.Any<string>())
                .Returns(callInfo =>
                {
                    resetEvent1.Set();
                    resetEvent2.WaitOne();

                    throw new InvalidCastException(errorMessage);
                });

            IImportErrorSerializer serializer = Substitute.For<IImportErrorSerializer>();
            serializer.Serialize(Arg.Any<long>(), Arg.Any<Exception>())
                .Returns(callInfo =>
                {
                    Exception ex = (Exception)callInfo[1];
                    return ex.Message;
                });

            Func<long, long> idGenerator = (i) => i;
            ImportResourceLoader loader = new ImportResourceLoader(integrationDataStoreClient, importResourceParser, serializer, NullLogger<ImportResourceLoader>.Instance);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            (Channel<ImportResource> outputChannel, Task importTask) = loader.LoadResources("http://dummy", 0, null, idGenerator, cancellationTokenSource.Token);

            resetEvent1.WaitOne();
            cancellationTokenSource.Cancel();
            resetEvent2.Set();

            await foreach (ImportResource resource in outputChannel.Reader.ReadAllAsync())
            {
                // do nothing.
            }

            try
            {
                await importTask;
                throw new InvalidOperationException();
            }
            catch (TaskCanceledException)
            {
                // Expected error
            }
            catch (OperationCanceledException)
            {
                // Expected error
            }
        }

        private async Task VerifyResourceLoaderAsync(int resourcCount, int batchSize, long startIndex)
        {
            long startId = 1;
            List<string> inputStrings = new List<string>();
            using MemoryStream stream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(stream);
            for (int i = 0; i < resourcCount; ++i)
            {
                string content = (i + startId).ToString();
                inputStrings.Add(content);
                await writer.WriteLineAsync(content);
            }

            await writer.FlushAsync();
            stream.Position = 0;

            IIntegrationDataStoreClient integrationDataStoreClient = Substitute.For<IIntegrationDataStoreClient>();
            integrationDataStoreClient.DownloadResource(Arg.Any<Uri>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(stream);

            IImportResourceParser importResourceParser = Substitute.For<IImportResourceParser>();
            importResourceParser.Parse(Arg.Any<string>())
                .Returns(callInfo =>
                {
                    string content = (string)callInfo[0];
                    return new ImportResource(0, 0, null, Encoding.UTF8.GetBytes(content));
                });

            IImportErrorSerializer serializer = Substitute.For<IImportErrorSerializer>();

            Func<long, long> idGenerator = (i) => startId + i;
            ImportResourceLoader loader = new ImportResourceLoader(integrationDataStoreClient, importResourceParser, serializer, NullLogger<ImportResourceLoader>.Instance);
            loader.MaxBatchSize = batchSize;

            (Channel<ImportResource> outputChannel, Task importTask) = loader.LoadResources("http://dummy", startIndex, null, idGenerator, CancellationToken.None);

            long currentIndex = startIndex;
            await foreach (ImportResource resource in outputChannel.Reader.ReadAllAsync())
            {
                string content = idGenerator(currentIndex++).ToString();
                Assert.Equal(content, Encoding.UTF8.GetString(resource.CompressedRawData));
            }

            await importTask;

            Assert.Equal(resourcCount, currentIndex);
        }
    }
}
