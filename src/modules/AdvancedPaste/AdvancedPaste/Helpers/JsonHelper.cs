// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using ManagedCommon;
using Newtonsoft.Json;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers
{
    internal static class JsonHelper
    {
        // List of supported CSV cdelimiters
        private static readonly char[] CsvDelimArry = [',', ';', '\t'];

        internal static string ToJsonFromXmlOrCsv(DataPackageView clipboardData)
        {
            Logger.LogTrace();

            if (clipboardData == null || !clipboardData.Contains(StandardDataFormats.Text))
            {
                Logger.LogWarning("Clipboard does not contain text data");
                return string.Empty;
            }

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            string text = Task.Run(async () =>
            {
                string plainText = await clipboardData.GetTextAsync() as string;
                return plainText;
            }).Result;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

            string jsonText = string.Empty;

            // Try convert XML
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(text);
                jsonText = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as xml", ex);
            }

            // Try convert CSV
            try
            {
                if (string.IsNullOrEmpty(jsonText))
                {
                    var csv = new List<string[]>();

                    string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length > 0)
                    {
                        GetCsvDelimiter(lines[0], out char delim, out int delimCount);

                        foreach (var line in lines)
                        {
                            // A CSV line is valid if we know the delimiter and if the delimiter occurs more or equal times in every line. (More because sometimes the delim occurs in a data string.)
                            if (delimCount > 0 && (line.Count(x => x == delim) >= delimCount))
                            {
                                csv.Add(line.Split(delim));
                            }
                            else
                            {
                                throw new FormatException("Invalid CSV format: Number of delimiters wrong in the current line.");
                            }
                        }

                        jsonText = JsonConvert.SerializeObject(csv, Newtonsoft.Json.Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as csv", ex);
            }

            // Try convert Plain Text
            try
            {
                if (string.IsNullOrEmpty(jsonText))
                {
                    var plainText = new List<string>();

                    foreach (var line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        plainText.Add(line);
                    }

                    jsonText = JsonConvert.SerializeObject(plainText, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as plain text", ex);
            }

            return string.IsNullOrEmpty(jsonText) ? text : jsonText;
        }

        private static void GetCsvDelimiter(in string firstLine, out char delimiter, out int delimiterCount)
        {
            Regex sepIdentifierRegex = new Regex("^sep=(.)$");
            delimiter = '\0'; // Unicode "null" character.
            delimiterCount = 0;

            var match = sepIdentifierRegex.Matches(firstLine)?[0].Value.Trim();
            if (match is not null)
            {
                delimiter = match[0];
                delimiterCount = firstLine.Count(x => x == match[0]);
            }
            else
            {
                foreach (char c in CsvDelimArry)
                {
                    int n = firstLine.Count(x => x == c);
                    if (n > delimiterCount)
                    {
                        delimiter = c;
                        delimiterCount = n;
                    }
                }
            }

            // If the delimiter count is 0, we can't detect it and the is no valid CSV line.
            if (delimiterCount == 0)
            {
                throw new FormatException("Invalid CSV format: Failed to detect the delimiter.");
            }
        }
    }
}
