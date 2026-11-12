using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using FakeItEasy;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    // Tests that capture the SelfLog must not run side by side, as the SelfLog is shared.
    [Collection("SelfLog")]
    public class AzureBlobStorageSinkUT
    {
        private readonly BlobServiceClient blobServiceClient = A.Fake<BlobServiceClient>();
        private readonly ICloudBlobProvider cloudBlobProvider = A.Fake<ICloudBlobProvider>();
        private readonly IAppendBlobBlockWriter blockWriter = A.Fake<IAppendBlobBlockWriter>();
        private readonly Dictionary<string, AppendBlobClient> blobClients = new Dictionary<string, AppendBlobClient>();

        // Lets a test make the provider hand out a rolled blob for a requested name.
        private readonly Dictionary<string, string> rolledNames = new Dictionary<string, string>();

        public AzureBlobStorageSinkUT()
        {
            A.CallTo(() => cloudBlobProvider.GetCloudBlobAsync(A<BlobServiceClient>._, A<string>._, A<string>._, A<bool>._, A<string>._, A<long?>._))
                .ReturnsLazily(call => Task.FromResult(GetBlobClient((string)call.Arguments[2])));
        }

        private AppendBlobClient GetBlobClient(string requestedName)
        {
            string name = rolledNames.TryGetValue(requestedName, out string rolledName) ? rolledName : requestedName;
            if (!blobClients.TryGetValue(name, out AppendBlobClient client))
            {
                client = A.Fake<AppendBlobClient>();
                A.CallTo(() => client.Name).Returns(name);
                blobClients[name] = client;
            }

            return client;
        }

        private AzureBlobStorageSink CreateSink(string storageFileName, int? retainedBlobCountLimit)
        {
            return new AzureBlobStorageSink(blobServiceClient, new AzureBlobStorageSinkOptions
            {
                StorageFileName = storageFileName,
                CloudBlobProvider = cloudBlobProvider,
                AppendBlobBlockWriter = blockWriter,
                RetainedBlobCountLimit = retainedBlobCountLimit,
                UseUtcTimezone = true
            });
        }

        private static LogEvent CreateEvent(int year, LogEventLevel level = LogEventLevel.Information)
        {
            return new LogEvent(new DateTimeOffset(year, 6, 1, 0, 0, 0, TimeSpan.Zero), level, null, new MessageTemplate("Hello", Enumerable.Empty<MessageTemplateToken>()), Enumerable.Empty<LogEventProperty>());
        }

        private void RetentionMustHaveHappened(int numberOfTimes)
        {
            A.CallTo(() => cloudBlobProvider.DeleteArchivedBlobsAsync(A<BlobServiceClient>._, A<string>._, A<string>._, A<int>._))
                .MustHaveHappened(numberOfTimes, Times.Exactly);
        }

        [Fact(DisplayName = "Should not look for old blobs when no retention limit is set")]
        public async Task NoRetentionWithoutLimit()
        {
            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", null);

            await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
            await sink.EmitBatchAsync(new[] { CreateEvent(2021) });

            RetentionMustHaveHappened(0);
        }

        [Fact(DisplayName = "Should look for old blobs once while batches go to the same blob")]
        public async Task RetentionRunsOncePerBlob()
        {
            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", 5);

            for (int i = 0; i < 10; i++)
            {
                await sink.EmitBatchAsync(new[] { CreateEvent(2020), CreateEvent(2020) });
            }

            RetentionMustHaveHappened(1);
        }

        [Fact(DisplayName = "Should look for old blobs again when the blob name changes")]
        public async Task RetentionRunsWhenBlobNameChanges()
        {
            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", 5);

            await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
            await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
            await sink.EmitBatchAsync(new[] { CreateEvent(2021) });
            await sink.EmitBatchAsync(new[] { CreateEvent(2021) });

            RetentionMustHaveHappened(2);
        }

        [Fact(DisplayName = "Should look for old blobs again when the blob is rolled over")]
        public async Task RetentionRunsWhenBlobIsRolled()
        {
            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", 5);

            await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
            rolledNames["log-2020.txt"] = "log-2020-001.txt";
            await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
            await sink.EmitBatchAsync(new[] { CreateEvent(2020) });

            RetentionMustHaveHappened(2);
        }

        [Fact(DisplayName = "Should not look for old blobs again when alternating between known blobs")]
        public async Task RetentionDoesNotRunForKnownBlobs()
        {
            AzureBlobStorageSink sink = CreateSink("{Level}.txt", 5);

            for (int i = 0; i < 5; i++)
            {
                await sink.EmitBatchAsync(new[] { CreateEvent(2020), CreateEvent(2020, LogEventLevel.Error), CreateEvent(2020) });
            }

            RetentionMustHaveHappened(1);
        }

        [Fact(DisplayName = "Should not fail the batch when deleting old blobs fails, and try again with the next batch")]
        public async Task RetentionFailureDoesNotFailBatch()
        {
            A.CallTo(() => cloudBlobProvider.DeleteArchivedBlobsAsync(A<BlobServiceClient>._, A<string>._, A<string>._, A<int>._))
                .Throws(new RequestFailedException(500, "Server {error}")).Once();

            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", 5);

            using (StringWriter selfLog = new StringWriter())
            {
                Debugging.SelfLog.Enable(TextWriter.Synchronized(selfLog));
                try
                {
                    await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
                    await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
                    await sink.EmitBatchAsync(new[] { CreateEvent(2020) });
                }
                finally
                {
                    Debugging.SelfLog.Disable();
                }
            }

            RetentionMustHaveHappened(2);
        }

        [Fact(DisplayName = "Should fail the batch when writing fails, so that Serilog retries it")]
        public async Task WriteFailureFailsBatch()
        {
            A.CallTo(() => blockWriter.WriteBlocksToAppendBlobAsync(A<AppendBlobClient>._, A<IEnumerable<string>>._))
                .Throws(new RequestFailedException(503, "Server busy"));

            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", 5);

            await Assert.ThrowsAsync<RequestFailedException>(() => sink.EmitBatchAsync(new[] { CreateEvent(2020) }));
            RetentionMustHaveHappened(0);
        }

        [Fact(DisplayName = "Should write the events of a batch to the blob they belong to")]
        public async Task WritesEventsToTheirBlob()
        {
            List<string> written = new List<string>();
            A.CallTo(() => blockWriter.WriteBlocksToAppendBlobAsync(A<AppendBlobClient>._, A<IEnumerable<string>>._))
                .Invokes((AppendBlobClient blob, IEnumerable<string> blocks) => written.Add(blob.Name + ":" + blocks.Count()));

            AzureBlobStorageSink sink = CreateSink("log-{yyyy}.txt", null);

            await sink.EmitBatchAsync(new[] { CreateEvent(2020), CreateEvent(2020), CreateEvent(2021) });

            Assert.Equal(new[] { "log-2020.txt:1", "log-2021.txt:1" }, written.ToArray());
        }
    }
}
