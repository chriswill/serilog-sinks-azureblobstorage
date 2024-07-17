using Serilog.Formatting.Json;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;

namespace Serilog.Sinks.AzureBlobStorage
{
    public class AzureBlobStorageSinkOptions
    {
        public ITextFormatter Formatter { get; set; } = new JsonFormatter();

        public string StorageContainerName { get; set; } = "logs";
        public string StorageFileName { get; set; } = "log.txt";
        public bool BypassContainerCreationValidation { get; set; } = false;
        public ICloudBlobProvider CloudBlobProvider { get; set; } = new DefaultCloudBlobProvider();
        public IAppendBlobBlockPreparer AppendBlobBlockPreparer { get; set; } = new DefaultAppendBlobBlockPreparer();
        public IAppendBlobBlockWriter AppendBlobBlockWriter { get; set; } = new DefaultAppendBlobBlockWriter();
        public string ContentType { get; set; } = "text/plain";
        public long? BlobSizeLimitBytes {get;set;}
        public int? RetainedBlobCountLimit { get; set; }
        public bool UseUtcTimezone { get; set; } = false;
    }
}
