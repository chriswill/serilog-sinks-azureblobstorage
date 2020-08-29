using System;
using System.Linq;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Produces a blob name using a given format string and a provided datetimeoffset.
    /// The format string must only contain the date time format characters:
    /// 'y', 'M', 'd', 'H', 'm'. Not all format characters must be used.
    /// If forward slashes are used in the format string, the logs will appear to be
    /// in folders in the azure storage explorer.
    /// </summary>
    public class BlobNameFactory
    {
        private readonly char[] DATE_FORMAT_ORDER = { 'y', 'M', 'd', 'H', 'm' };
        private readonly string baseBlobName;

        public BlobNameFactory(string baseBlobName)
        {
            this.baseBlobName = baseBlobName ?? throw new ArgumentNullException(nameof(baseBlobName));

            ValidatedBlobName();
        }

        public string GetBlobName(DateTimeOffset dtoToApply)
        {
            // Create copy of the base name
            string defaultName = (string)baseBlobName.Clone();

            // Find first date format by finding first set of braces
            var openBraceIndex = defaultName.IndexOf('{');
            var closeBraceIndex = defaultName.IndexOf('}');
            
            while (openBraceIndex != -1 && closeBraceIndex != -1)
            {
                // Get date format inside the braces
                var dateFormat = defaultName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);

                // Replace braces and supplied format with formatted date time
                defaultName = defaultName.Remove(openBraceIndex, closeBraceIndex - openBraceIndex + 1);
                defaultName = defaultName.Insert(openBraceIndex, dtoToApply.ToString(dateFormat));

                // Find next set of braces
                openBraceIndex = defaultName.IndexOf('{');
                closeBraceIndex = defaultName.IndexOf('}');
            }

            return defaultName;
        }

        /// <summary>
        /// Validate the base blob name provided.
        /// </summary>
        private void ValidatedBlobName()
        {
            int i = 0;
            while (i < baseBlobName.Length)
            {
                var openBraceIndex = baseBlobName.IndexOf('{', i);

                if (openBraceIndex < 0)
                {
                    break;
                }

                // Get the date format characters within the current pair of curly braces.
                var closeBraceIndex = baseBlobName.IndexOf('}', openBraceIndex);
                var dateFormat = baseBlobName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);

                //// Check all characters in the date format string to make sure
                //// they exist in currently expected format character list.
                var charList = dateFormat.ToCharArray();
                if (charList.Any(c => !DATE_FORMAT_ORDER.Contains(c)))
                    throw new ArgumentException($"{nameof(baseBlobName)} contains unexpected format character.");

                i = closeBraceIndex + 1;
            }
        }
    }
}
