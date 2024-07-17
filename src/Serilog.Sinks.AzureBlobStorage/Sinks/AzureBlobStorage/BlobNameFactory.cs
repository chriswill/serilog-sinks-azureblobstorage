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
        private readonly char[] dateFormatChars = { 'y', 'M', 'd', 'H', 'm' };
        private readonly List<string> supportedProperties = new List<string>{"Level"};
        private readonly string baseBlobName;
        
        public BlobNameFactory(string baseBlobName)
        {
            this.baseBlobName = baseBlobName ?? throw new ArgumentNullException(nameof(baseBlobName));
        }

        public string GetBlobName(DateTimeOffset dtTimeOffset, LogEventLevel? logLevel = null, IReadOnlyDictionary<string, LogEventPropertyValue> properties = null, bool useUtcTimeZone = false)
        {
            ValidateBlobName(logLevel, properties);

            // Create copy of the base name
            string defaultName = (string)baseBlobName.Clone();
            // Find first date format by finding first set of braces
            int openBraceIndex = defaultName.IndexOf('{');
            int closeBraceIndex = defaultName.IndexOf('}');

            while (openBraceIndex != -1 && closeBraceIndex != -1)
            {
                // Get date format inside the braces
                string nameFormat = defaultName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);

                // Replace braces and supplied format with formatted date time
                defaultName = defaultName.Remove(openBraceIndex, closeBraceIndex - openBraceIndex + 1);

                char[] charList = nameFormat.ToCharArray();
                if (charList.All(c => dateFormatChars.Contains(c)))
                {
                    defaultName = defaultName.Insert(openBraceIndex, useUtcTimeZone ? dtTimeOffset.UtcDateTime.ToString(nameFormat) : dtTimeOffset.ToString(nameFormat));
                }
                else if ("Level".Equals(nameFormat))
                {
                    defaultName = defaultName.Insert(openBraceIndex, logLevel.ToString() ?? "Unknown");
                }
                else
                {
                    if (properties != null && properties.ContainsKey(nameFormat))
                    {
                        if (properties[nameFormat] is ScalarValue)
                        {
                            string propertyValue = ((ScalarValue)properties[nameFormat]).Value.ToString();
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
        private void ValidateBlobName(LogEventLevel? logLevel = null, IReadOnlyDictionary<string, LogEventPropertyValue> properties = null)
        {
            try
            {
                int i = 0;
                while (i < baseBlobName.Length)
                {
                    int openBraceIndex = baseBlobName.IndexOf('{', i);

                    if (openBraceIndex < 0)
                    {
                        break;
                    }

                    // Get the date format characters within the current pair of curly braces.
                    int closeBraceIndex = baseBlobName.IndexOf('}', openBraceIndex);
                    string dateFormat = baseBlobName.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);

                    //// Check all characters in the date format string to make sure
                    //// they exist in currently expected format character list.
                    char[] charList = dateFormat.ToCharArray();
                    List<char> supportedPropertyChars = new List<char>();
                    List<char> propertyChars = new List<char>();
                    if (properties != null)
                    {
                        foreach (KeyValuePair<string, LogEventPropertyValue> logEventPropertyValue in properties)
                        {
                            foreach (char c in logEventPropertyValue.Key.Where(c => !propertyChars.Contains(c)))
                            {
                                propertyChars.Add(c);
                            }
                        }
                    }

                    foreach (string s in supportedProperties)
                    {
                        foreach (char c in s.ToCharArray())
                        {
                            if (!supportedPropertyChars.Contains(c))
                            {
                                supportedPropertyChars.Add(c);
                            }
                        }
                    }

                    if (charList.Any(c => !dateFormatChars.Contains(c)) && charList.Any(c => !propertyChars.Contains(c)) && charList.Any(c => !supportedPropertyChars.Contains(c)))
                    {
                        throw new ArgumentException($"{nameof(baseBlobName)} contains unexpected format character.");
                    }

                    i = closeBraceIndex + 1;
                }
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Blob pattern was not in a parsable format: {0} {1}", ex.Message, ex.StackTrace);
                throw;
            }
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
