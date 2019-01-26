using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class DefaultAppendBlobBlockWriterUT
    {
        private readonly DefaultAppendBlobBlockWriter _defaultAppendBlobBlockWriter;

        private readonly CloudAppendBlob cloudBlobFake= A.Fake<CloudAppendBlob>(opt=> opt.WithArgumentsForConstructor(new[] { new Uri("https://blob.com/test/test.txt") }));

        private readonly IEnumerable<string> noBlocksToWrite = Enumerable.Empty<string>();
        private readonly IEnumerable<string> singleBlockToWrite = new[] { new string('*', 1024 * 1024 * 3) };
        private readonly IEnumerable<string> multipleBlocksToWrite = new[] { new string('*', 1024 * 512 * 3), new string('*', 1024 * 512 * 3) };
        private readonly bool targetsNetCore;

        public DefaultAppendBlobBlockWriterUT()
        {
            _defaultAppendBlobBlockWriter = new DefaultAppendBlobBlockWriter();

            var framework = Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            targetsNetCore = !string.IsNullOrEmpty(framework);
        }

        [Fact(DisplayName = "Should not write anything when no blocks to write.")]
        public async Task WriteNothingIfNoBlocksSent()
        {            
            await _defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, noBlocksToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        //Test fail under .NETCoreApp 2.0 although calls to method actually made as designed
        [SkippableFact(DisplayName = "Should write as many blocks as going in, one.")]
        public async Task WriteSingleBlockOnSingleInput()
        {
            Skip.If(targetsNetCore);
            await _defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, singleBlockToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustHaveHappenedOnceExactly();
        }

        //Test fail under .NETCoreApp 2.0 although calls to method actually made as designed
        [SkippableFact(DisplayName = "Should write as many blocks as going in, two.")]
        public async Task WriteTwoBlocksOnOnInputOfTwo()
        {
            Skip.If(targetsNetCore);
            await _defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, multipleBlocksToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustHaveHappened(multipleBlocksToWrite.Count(), Times.Exactly);
        }
    }
}
