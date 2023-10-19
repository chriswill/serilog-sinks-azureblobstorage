using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        public string GetBlobName(DateTimeOffset dtoToApply, IReadOnlyDictionary<string, LogEventPropertyValue> properties = null, bool useUTCTimeZone = false)
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

                var charList = dateFormat.ToCharArray();
                if (charList.All(c => DATE_FORMAT_ORDER.Contains(c)))
                {
                    defaultName = defaultName.Insert(openBraceIndex, useUTCTimeZone ? dtoToApply.UtcDateTime.ToString(dateFormat) : dtoToApply.ToString(dateFormat));
                }
                else
                {
                    if (properties != null && properties.ContainsKey(dateFormat))
                    {
                        if (properties[dateFormat] is ScalarValue)
                        {
                            string propertyValue = ((ScalarValue)properties[dateFormat]).Value.ToString();
                            defaultName = defaultName.Insert(openBraceIndex, propertyValue);
                        }
                    }
                }

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
            try
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
                    //if (charList.Any(c => !DATE_FORMAT_ORDER.Contains(c)))
                    //    throw new ArgumentException($"{nameof(baseBlobName)} contains unexpected format character.");

                    i = closeBraceIndex + 1;
                }
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Blob pattern was not in a parsable format: {0} {1}", ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        /// Gets a blob name format (parsable by DateTime) generated based on the blob name passed to the logger, 
        /// to identify the blobs in the container created by the logger 
        /// and only consider those while counting for deletion of older blobs.
        /// </summary>
        public string GetBlobNameFormat()
        {
            // Create copy of the base name
            string defaultName = (string)baseBlobName.Clone();
            string pattern = "{(.*?)}";

            // get all words between curly braces ({<date time format>})
            var dateFormats = Regex.Matches(defaultName, pattern)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToArray();

            // gets all dateformat strings and non-dateformat strings
            string[] fileNameSplit = Regex.Split(defaultName, pattern);

            List<string> fileNameParts = new List<string>();
            // For all "non-dateformat" strings, it encloses in a single-quotes as a string literal
            foreach (var fileNamePart in fileNameSplit)
            {
                if (dateFormats.Contains(fileNamePart))
                {
                    fileNameParts.Add(fileNamePart);
                }
                else
                {
                    fileNameParts.Add("'" + fileNamePart + "'");
                }
            }

            string fileFormatRegex = String.Join("", fileNameParts.ToArray());
            return fileFormatRegex;
        }
    }
}
