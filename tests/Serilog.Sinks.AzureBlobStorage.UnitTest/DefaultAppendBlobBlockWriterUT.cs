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
    /// <summary>
    /// Dot Net Core throws an exception of System.MissingMethodException : Method not found: 'System.Threading.Tasks.Task`1<Int64> Microsoft.Azure.Storage.Blob.CloudAppendBlob.AppendBlockAsync(System.IO.Stream)'.
    /// </summary>
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

        [SkippableFact(DisplayName = "Should not write anything when no blocks to write.")]
        public async Task WriteNothingIfNoBlocksSent()
        {
            Skip.If(targetsNetCore);
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, noBlocksToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        //Test fail under .NETCoreApp 2.0 although calls to method actually made as designed
        [SkippableFact(DisplayName = "Should write as many blocks as going in, one.")]
        public async Task WriteSingleBlockOnSingleInput()
        {
            Skip.If(targetsNetCore);
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, singleBlockToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustHaveHappenedOnceExactly();
        }

        //Test fail under .NETCoreApp 2.0 although calls to method actually made as designed
        [SkippableFact(DisplayName = "Should write as many blocks as going in, two.")]
        public async Task WriteTwoBlocksOnOnInputOfTwo()
        {
            Skip.If(targetsNetCore);
            await defaultAppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(cloudBlobFake, multipleBlocksToWrite);

            A.CallTo(() => cloudBlobFake.AppendBlockAsync(A<Stream>.Ignored, A<string>.Ignored)).MustHaveHappened(multipleBlocksToWrite.Count(), Times.Exactly);
        }
    }
}
