using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FakeItEasy;
using Azure.Storage.Blobs;
using Xunit;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;
using System.Threading;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    // Tests that capture the SelfLog must not run side by side, as the SelfLog is shared.
    [Collection("SelfLog")]
    public class DefaultAppendBlobBlockWriterUT
    {
        private readonly DefaultAppendBlobBlockWriter defaultAppendBlobBlockWriter;
        private readonly AppendBlobClient blobClientFake = A.Fake<AppendBlobClient>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://blob.com/test/test.txt"), new BlobClientOptions() }));

        private readonly IEnumerable<string> noBlocksToWrite = Enumerable.Empty<string>();
        private readonly IEnumerable<string> singleBlockToWrite = new[] { new string('*', 1024 * 1024 * 3) };
        private readonly IEnumerable<string> multipleBlocksToWrite = new[] { new string('*', 1024 * 512 * 3), new string('*', 1024 * 512 * 3) };

        public DefaultAppendBlobBlockWriterUT()
        {
            defaultAppendBlobBlockWriter = new DefaultAppendBlobBlockWriter();

            var framework = Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;
        }

        [Fact(DisplayName = "Should write nothing if not blocks were sent")]
        public async Task WriteNothingIfNoBlocksSent()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, noBlocksToWrite);

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<AppendBlobAppendBlockOptions>.Ignored,
                A<CancellationToken>.Ignored)).MustNotHaveHappened();
        }

        [Fact(DisplayName = "Should write single block on single input")]
        public async Task WriteSingleBlockOnSingleInput()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, singleBlockToWrite);

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<AppendBlobAppendBlockOptions>.Ignored,
                A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Fact(DisplayName = "Should write two block on input of two")]
        public async Task WriteTwoBlocksOnOnInputOfTwo()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, multipleBlocksToWrite);

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<AppendBlobAppendBlockOptions>.Ignored,
                A<CancellationToken>.Ignored)).MustHaveHappened(multipleBlocksToWrite.Count(), Times.Exactly);
        }

        [Fact(DisplayName = "Should rethrow a failure to append a block, so that the batch can be retried")]
        public async Task RethrowAppendFailure()
        {
            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<AppendBlobAppendBlockOptions>.Ignored,
                A<CancellationToken>.Ignored)).Throws(new Azure.RequestFailedException(503, "Server {busy}"));

            using (StringWriter selfLog = new StringWriter())
            {
                Debugging.SelfLog.Enable(TextWriter.Synchronized(selfLog));
                try
                {
                    await Assert.ThrowsAsync<Azure.RequestFailedException>(() => defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, multipleBlocksToWrite));
                }
                finally
                {
                    Debugging.SelfLog.Disable();
                }

                Assert.Contains("Server {busy}", selfLog.ToString());
            }

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<AppendBlobAppendBlockOptions>.Ignored,
                A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        }
    }
}
