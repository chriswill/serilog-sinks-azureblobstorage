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
    public class DefaultAppendBlobBlockWriterUT
    {
        private readonly DefaultAppendBlobBlockWriter defaultAppendBlobBlockWriter;

        private readonly AppendBlobClient blobClientFake = A.Fake<AppendBlobClient>(opt => opt.WithArgumentsForConstructor(new[] { new Uri("https://blob.com/test/test.txt"), null }));

        private readonly IEnumerable<string> noBlocksToWrite = Enumerable.Empty<string>();
        private readonly IEnumerable<string> singleBlockToWrite = new[] { new string('*', 1024 * 1024 * 3) };
        private readonly IEnumerable<string> multipleBlocksToWrite = new[] { new string('*', 1024 * 512 * 3), new string('*', 1024 * 512 * 3) };
        private readonly bool targetsNetCore;

        public DefaultAppendBlobBlockWriterUT()
        {
            defaultAppendBlobBlockWriter = new DefaultAppendBlobBlockWriter();

            var framework = Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            targetsNetCore = !string.IsNullOrEmpty(framework);
        }

        [Fact(DisplayName = "Should write nothing if not blocks were sent")]
        public async Task WriteNothingIfNoBlocksSent()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, noBlocksToWrite);

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<byte[]>.Ignored,
                A<AppendBlobRequestConditions>.Ignored,
                A<IProgress<long>>.Ignored,
                A<CancellationToken>.Ignored)).MustNotHaveHappened();
        }

        [Fact(DisplayName = "Should write single block on single input")]
        public async Task WriteSingleBlockOnSingleInput()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, singleBlockToWrite);

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<byte[]>.Ignored,
                A<AppendBlobRequestConditions>.Ignored,
                A<IProgress<long>>.Ignored,
                A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Fact(DisplayName = "Should write two block on input of two")]
        public async Task WriteTwoBlocksOnOnInputOfTwo()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blobClientFake, multipleBlocksToWrite);

            A.CallTo(() => blobClientFake.AppendBlockAsync(
                A<Stream>.Ignored,
                A<byte[]>.Ignored,
                A<AppendBlobRequestConditions>.Ignored,
                A<IProgress<long>>.Ignored,
                A<CancellationToken>.Ignored)).MustHaveHappened(multipleBlocksToWrite.Count(), Times.Exactly);
        }
    }
}
