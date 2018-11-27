using System;
using System.Text;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Produces a blob name using a given format string and a provided datetimeoffset.
    /// The format string must only contain the date time foramt characters in the
    /// following order: 'y', 'M', 'd', 'H', 'm'. Not all format characters must be used.
    /// If forward slashes are used in the format string, the logs will appear to be
    /// in folders in the azure storage explorer.
    /// </summary>
    public class BlobNameFactory
    {
        readonly char[] DATE_FORMAT_ORDER = { 'y', 'M', 'd', 'H', 'm' };
        string baseBlobName;

        public BlobNameFactory(string baseBlobName)
        {
            this.baseBlobName = baseBlobName ?? throw new ArgumentNullException(nameof(baseBlobName));

            ValidatedBlobName();
        }

        public string GetBlobName(DateTimeOffset dtoToApply)
        {
            string blobName = "";
            StringBuilder sb = new StringBuilder();

            // Example:
            // baseBlobName = webhook/{yyyy}/{MM}/{dd}/{HH}.txt
            // on November 26, 2018 at 17:52
            // blobName = webhook/2018/11/26/17.txt
            int i = 0;
            while (i < baseBlobName.Length)
            {
                var openBraceIndex = baseBlobName.IndexOf('{', i);

                if (openBraceIndex < 0)
                {
                    sb.Append(baseBlobName.Substring(i));
                    break;
                }
                else
                {
                    if (i != openBraceIndex)
                    {
                        sb.Append(baseBlobName.Substring(i, openBraceIndex - i));
                        i = openBraceIndex;
                    }
                    
                    var closeBraceIndex = baseBlobName.IndexOf('}', openBraceIndex);
                    var dateFormat = baseBlobName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                    sb.Append(dtoToApply.ToString(dateFormat));
                    i = closeBraceIndex + 1;
                }
            }

            blobName = sb.ToString();

            return blobName;
        }

        /// <summary>
        /// Validate the base blob name provided.
        /// </summary>
        private void ValidatedBlobName()
        {
            int j = 0;
            int i = 0;
            while (i < baseBlobName.Length)
            {
                var openBraceIndex = baseBlobName.IndexOf('{', i);

                if (openBraceIndex < 0)
                {
                    break;
                }
                else
                {
                    // Get the date format characters within the current pair of curly braces.
                    var closeBraceIndex = baseBlobName.IndexOf('}', openBraceIndex);
                    var dateFormat = baseBlobName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);

                    // Keep checking the date date format string until all characters within have been verified.
                    int lastIndex = 0;
                    do
                    {
                        // The index of the DATE_FORMAT_ORDER array is beyond the last element,
                        // by default the base blob name is invalid.
                        if (j >= DATE_FORMAT_ORDER.Length)
                            throw new ArgumentException($"{nameof(baseBlobName)} has unexpected date format characters.");

                        // Get the last index of the currently expected format character. If that
                        // character is not found, then the characters are out of order or an
                        // unexpected character was provided.
                        lastIndex = dateFormat.LastIndexOf(DATE_FORMAT_ORDER[j++]);
                        if (lastIndex < 0)
                            throw new ArgumentException($"{nameof(baseBlobName)} has unexpectently encountered the format character '{DATE_FORMAT_ORDER[j - 1]}'.");

                    } while (lastIndex + 1 < dateFormat.Length);

                    i = closeBraceIndex + 1;
                }
            }
        }
    }
}
