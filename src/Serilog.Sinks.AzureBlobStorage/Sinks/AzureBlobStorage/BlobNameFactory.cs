using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly char[] dateFormatChars = { 'y', 'M', 'd', 'H', 'm' };
        private readonly List<string> supportedProperties = new List<string>{"Level"};
        private readonly string baseBlobName;

        // The base name split into literal text and {tokens}, or null if the base name could not be parsed.
        private readonly List<NamePart> nameParts;

        private sealed class NamePart
        {
            public string Text;
            public bool IsToken;
            public bool IsDateFormat;
            public bool UsesOnlySupportedPropertyChars;
        }

        public BlobNameFactory(string baseBlobName)
        {
            this.baseBlobName = baseBlobName ?? throw new ArgumentNullException(nameof(baseBlobName));
            nameParts = ParseNameParts(baseBlobName);
        }

        /// <summary>
        /// Splits the base name once, so that this work is not repeated for every log event.
        /// </summary>
        private List<NamePart> ParseNameParts(string name)
        {
            List<NamePart> parts = new List<NamePart>();
            int position = 0;

            while (position < name.Length)
            {
                int openBraceIndex = name.IndexOf('{', position);
                if (openBraceIndex < 0)
                {
                    break;
                }

                int closeBraceIndex = name.IndexOf('}', openBraceIndex);
                if (closeBraceIndex < 0)
                {
                    // A brace was opened but never closed
                    return null;
                }

                if (openBraceIndex > position)
                {
                    parts.Add(new NamePart { Text = name.Substring(position, openBraceIndex - position) });
                }

                string token = name.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                parts.Add(new NamePart
                {
                    Text = token,
                    IsToken = true,
                    IsDateFormat = token.All(c => dateFormatChars.Contains(c)),
                    UsesOnlySupportedPropertyChars = token.All(c => supportedProperties.Any(p => p.IndexOf(c) >= 0))
                });

                position = closeBraceIndex + 1;
            }

            if (position < name.Length)
            {
                parts.Add(new NamePart { Text = name.Substring(position) });
            }

            return parts;
        }

        public string GetBlobName(DateTimeOffset dtTimeOffset, LogEventLevel? logLevel = null, IReadOnlyDictionary<string, LogEventPropertyValue> properties = null, bool useUtcTimeZone = false)
        {
            ValidateBlobName(properties);

            StringBuilder blobName = new StringBuilder(baseBlobName.Length + 16);

            foreach (NamePart part in nameParts)
            {
                if (!part.IsToken)
                {
                    blobName.Append(part.Text);
                }
                else if (part.IsDateFormat)
                {
                    // Replace braces and supplied format with formatted date time
                    blobName.Append(useUtcTimeZone ? dtTimeOffset.UtcDateTime.ToString(part.Text) : dtTimeOffset.ToString(part.Text));
                }
                else if ("Level".Equals(part.Text))
                {
                    blobName.Append(logLevel.ToString());
                }
                else if (properties != null && properties.TryGetValue(part.Text, out LogEventPropertyValue propertyValue) && propertyValue is ScalarValue scalarValue)
                {
                    blobName.Append(scalarValue.Value);
                }
            }

            return blobName.ToString();
        }

        /// <summary>
        /// Validate the base blob name provided.
        /// </summary>
        private void ValidateBlobName(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            try
            {
                if (nameParts == null)
                {
                    throw new ArgumentException($"{nameof(baseBlobName)} contains a brace that is not closed.");
                }

                foreach (NamePart part in nameParts)
                {
                    // Date formats and the supported properties were recognised when the name was parsed.
                    if (!part.IsToken || part.IsDateFormat || part.UsesOnlySupportedPropertyChars)
                    {
                        continue;
                    }

                    //// Check all characters in the token to make sure
                    //// they exist in the names of the properties of the log event.
                    foreach (char c in part.Text)
                    {
                        if (!IsPropertyNameChar(c, properties))
                        {
                            throw new ArgumentException($"{nameof(baseBlobName)} contains unexpected format character.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine("Blob pattern was not in a parsable format: {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
        }

        private static bool IsPropertyNameChar(char c, IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            if (properties == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, LogEventPropertyValue> property in properties)
            {
                if (property.Key.IndexOf(c) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        
        public string GetBlobRegex()
        {
            // Create copy of the base name
            string defaultName = (string)baseBlobName.Clone();
            string pattern = "{(.*?)}";

            // get all words between curly braces ({<date time format>})
            string[] customMatches = Regex.Matches(defaultName, pattern)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToArray();

            // gets all dateformat strings and non-dateformat strings
            string[] fileNameSplit = Regex.Split(defaultName, pattern);

            var patterns = new Dictionary<string, string>
            {
                { "y", @"\d{1,4}" },
                { "M", @"\d{1,2}" },
                { "d", @"\d{1,2}" },
                { "H", @"\d{1,2}" },
                { "m", @"\d{1,2}" }
            };

            List<string> fileNameParts = new List<string>();

            foreach (var fileNamePart in fileNameSplit)
            {

                if (fileNamePart.Equals(string.Empty)) continue;

                char[] charList = fileNamePart.ToCharArray();

                if (supportedProperties.Contains(fileNamePart))
                {
                    fileNameParts.Add("\\S*");
                }
                else if (charList.All(c => dateFormatChars.Contains(c)))
                {
                    fileNameParts.Add(@"\d{" + fileNamePart.Length + "}");
                }
                else if (customMatches.Contains(fileNamePart))
                {
                    fileNameParts.Add("\\S*");
                }
                else
                {
                    fileNameParts.Add(Regex.Escape(fileNamePart));
                }
            }

            string fileFormatRegex = string.Join("", fileNameParts.ToArray());
            return fileFormatRegex;
        }
    }
}
