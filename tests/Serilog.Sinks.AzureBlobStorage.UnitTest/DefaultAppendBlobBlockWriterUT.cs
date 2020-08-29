using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Azure.Storage.Blob;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class DefaultAppendBlobBlockWriterUT
    {
        private readonly DefaultAppendBlobBlockWriter defaultAppendBlobBlockWriter;

        private readonly CloudAppendBlob cloudBlobFake= A.Fake<CloudAppendBlob>(opt=> opt.WithArgumentsForConstructor(new[] { new Uri("https://blob.com/test/test.txt") }));

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
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, noBlocksToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [Fact(DisplayName = "Should write single block on single input")]
        public async Task WriteSingleBlockOnSingleInput()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, singleBlockToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Fact(DisplayName = "Should write two block on input of two")]
        public async Task WriteTwoBlocksOnOnInputOfTwo()
        {
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, multipleBlocksToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustHaveHappened(multipleBlocksToWrite.Count(), Times.Exactly);
        }
    }
}
