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
        // Ini parts regex
        private static readonly Regex IniSectionNameRegex = new Regex(@"^\[(.+)\]");
        private static readonly Regex IniValueLineRegex = new Regex(@"(.+?)\s*=\s*(.*)");

        // List of supported CSV delimiters and Regex to detect separator property
        private static readonly char[] CsvDelimArry = [',', ';', '\t'];
        private static readonly Regex CsvSepIdentifierRegex = new Regex(@"^sep=(.)$", RegexOptions.IgnoreCase);

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
                Logger.LogDebug("Converted from XML.");
                jsonText = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as xml", ex);
            }

            // Try convert Ini
            // (Must come before CSV that ini is not false detected as CSV.)
            try
            {
                if (string.IsNullOrEmpty(jsonText))
                {
                    var ini = new Dictionary<string, Dictionary<string, string>>();
                    var lastSectionName = string.Empty;

                    string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    // Skipp comment lines.
                    // (Comments are lines that starts with a semicolon.
                    // This also skips commented key-value-pairs.)
                    lines = lines.Where(l => !l.StartsWith(';')).ToArray();

                    // Validate content as ini
                    // (First line is a section name and second line is a section name or a key-value-pair.
                    // For the second line we check both, in case the first ini section is empty.)
                    if (lines.Length >= 2 && IniSectionNameRegex.IsMatch(lines[0]) &&
                        (IniSectionNameRegex.IsMatch(lines[1]) || IniValueLineRegex.IsMatch(lines[1])))
                    {
                        // Parse and convert Ini
                        foreach (string line in lines)
                        {
                            Match lineSectionNameCheck = IniSectionNameRegex.Match(line);
                            Match lineKeyValuePairCheck = IniValueLineRegex.Match(line);

                            if (lineSectionNameCheck.Success)
                            {
                                // Section name (Group 1)
                                lastSectionName = lineSectionNameCheck.Groups[1].Value.Trim();
                                if (string.IsNullOrWhiteSpace(lastSectionName))
                                {
                                    throw new FormatException("Invalid ini file format: Empty section name.");
                                }

                                ini.Add(lastSectionName, new Dictionary<string, string>());
                            }
                            else if (!lineKeyValuePairCheck.Success)
                            {
                                // Fail if it is not a key-value-pair (and was not detected as section name before).
                                throw new FormatException("Invalid ini file format: Invalid line.");
                            }
                            else
                            {
                                // Key-value-pair (Group 1=Key; Group 2=Value)
                                string iniKeyName = lineKeyValuePairCheck.Groups[1].Value.Trim();
                                if (string.IsNullOrWhiteSpace(iniKeyName))
                                {
                                    throw new FormatException("Invalid ini file format: Empty value name (key).");
                                }

                                string iniValueData = lineKeyValuePairCheck.Groups[2].Value;
                                ini[lastSectionName].Add(iniKeyName, iniValueData);
                            }
                        }

                        // Convert to JSON
                        Logger.LogDebug("Converted from Ini.");
                        jsonText = JsonConvert.SerializeObject(ini, Newtonsoft.Json.Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as ini", ex);
            }

            // Try convert CSV
            try
            {
                if (string.IsNullOrEmpty(jsonText))
                {
                    var csv = new List<string[]>();

                    string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    // Detect the csv delimiter and the count of occurrence based on the first two csv lines.
                    GetCsvDelimiter(lines, out char delim, out int delimCount);

                    foreach (var line in lines)
                    {
                        // If line is separator property line, then skip it
                        if (CsvSepIdentifierRegex.IsMatch(line))
                        {
                            continue;
                        }

                        // A CSV line is valid, if the delimiter occurs more or equal times in every line compared to the first data line. (More because sometimes the delimiter occurs in a data string.)
                        if (line.Count(x => x == delim) >= delimCount)
                        {
                            csv.Add(line.Split(delim));
                        }
                        else
                        {
                            throw new FormatException("Invalid CSV format: Number of delimiters wrong in the current line.");
                        }
                    }

                    Logger.LogDebug("Convert from csv.");
                    jsonText = JsonConvert.SerializeObject(csv, Newtonsoft.Json.Formatting.Indented);
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

                    Logger.LogDebug("Convert from plain text.");
                    jsonText = JsonConvert.SerializeObject(plainText, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as plain text", ex);
            }

            return string.IsNullOrEmpty(jsonText) ? text : jsonText;
        }

        private static void GetCsvDelimiter(in string[] csvLines, out char delimiter, out int delimiterCount)
        {
            delimiter = '\0'; // Unicode "null" character.
            delimiterCount = 0;

            if (csvLines.Length > 1)
            {
                // Try to select the delimiter based on the separator property.
                Match matchChar = CsvSepIdentifierRegex.Match(csvLines[0]);
                if (matchChar.Success)
                {
                    // We can do matchChar[0] as the match only returns one character.
                    // We get the count from the second line, as the first one only contains the character definition and not a CSV data line.
                    char delimChar = matchChar.Groups[1].Value.Trim()[0];
                    delimiter = delimChar;
                    delimiterCount = csvLines[1].Count(x => x == delimChar);
                }
            }

            if (csvLines.Length > 0 && delimiterCount == 0)
            {
                // Try to select the correct delimiter based on the first two CSV lines from a list of predefined delimiters.
                foreach (char c in CsvDelimArry)
                {
                    int cntFirstLine = csvLines[0].Count(x => x == c);
                    int cntNextLine = 0; // Default to 0 that the 'second line' check is always true.

                    // Additional count if we have more than one line
                    if (csvLines.Length >= 2)
                    {
                        cntNextLine = csvLines[1].Count(x => x == c);
                    }

                    // The delimiter is found if the count is bigger as from the last selected delimiter
                    // and if the next csv line does not exist or has the same number or more occurrences of the delimiter.
                    // (We check the next line to prevent false positives.)
                    if (cntFirstLine > delimiterCount && (cntNextLine == 0 || cntNextLine >= cntFirstLine))
                    {
                        delimiter = c;
                        delimiterCount = cntFirstLine;
                    }
                }
            }

            // If the delimiter count is 0, we can't detect it and it is no valid CSV.
            if (delimiterCount == 0)
            {
                throw new FormatException("Invalid CSV format: Failed to detect the delimiter.");
            }
        }
    }
}
