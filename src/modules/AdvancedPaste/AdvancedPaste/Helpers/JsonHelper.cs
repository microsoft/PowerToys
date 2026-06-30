// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        // CSV: Split on every occurrence of the delimiter except if it is enclosed by " and ignore two " as escaped "
        private static readonly string CsvDelimSepRegexStr = @"(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";

        // CSV: Regex to remove/replace quotation marks
        private static readonly Regex CsvRemoveSingleQuotationMarksRegex = new Regex(@"^""(?!"")|(?<!"")""$|^""""$");
        private static readonly Regex CsvRemoveStartAndEndQuotationMarksRegex = new Regex(@"^""(?=(""{2})+)|(?<=(""{2})+)""$");
        private static readonly Regex CsvReplaceDoubleQuotationMarksRegex = new Regex(@"""{2}");

        private static bool IsJson(string text)
        {
            try
            {
                _ = JsonDocument.Parse(text);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static async Task<string> ToJsonFromXmlOrCsvAsync(DataPackageView clipboardData)
        {
            Logger.LogTrace();

            if (!clipboardData.Contains(StandardDataFormats.Text))
            {
                Logger.LogWarning("Clipboard does not contain text data");
                return string.Empty;
            }

            var text = await clipboardData.GetTextAsync();
            string jsonText = string.Empty;

            // If the text is already JSON, return it
            if (IsJson(text))
            {
                return text;
            }

            // Try convert XML
            try
            {
                XmlDocument doc = new();
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
                    var csv = new List<IEnumerable<string>>();

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

                        // A CSV line is valid, if the delimiter occurs equal times in every line compared to the first data line
                        // and if every line contains no or an even count of quotation marks.
                        if (Regex.Count(line, delim + CsvDelimSepRegexStr) == delimCount && int.IsEvenInteger(line.Count(x => x == '"')))
                        {
                            string[] dataCells = Regex.Split(line, delim + CsvDelimSepRegexStr, RegexOptions.IgnoreCase);
                            csv.Add(dataCells.Select(x => ReplaceQuotationMarksInCsvData(x)));
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
                    delimiterCount = Regex.Count(csvLines[1], delimChar + CsvDelimSepRegexStr, RegexOptions.IgnoreCase);
                }
            }

            if (csvLines.Length > 0 && delimiterCount == 0)
            {
                // Try to select the correct delimiter based on the first two CSV lines from a list of predefined delimiters.
                foreach (char c in CsvDelimArry)
                {
                    int cntFirstLine = Regex.Count(csvLines[0], c + CsvDelimSepRegexStr, RegexOptions.IgnoreCase);
                    int cntNextLine = 0; // Default to 0 that the 'second line' check is always true.

                    // Additional count if we have more than one line
                    if (csvLines.Length >= 2)
                    {
                        cntNextLine = Regex.Count(csvLines[1], c + CsvDelimSepRegexStr, RegexOptions.IgnoreCase);
                    }

                    // The delimiter is found if the count is bigger as from the last selected delimiter
                    // and if the next csv line does not exist or has the same number of occurrences of the delimiter.
                    // (We check the next line to prevent false positives.)
                    if (cntFirstLine > delimiterCount && (cntNextLine == 0 || cntNextLine == cntFirstLine))
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

        /// <summary>
        /// Remove and replace quotation marks used as control sequences. (Enclosing quotation marks and escaping quotation marks.)
        /// </summary>
        /// <param name="str">CSV cell data to manipulate.</param>
        /// <returns>Manipulated string.</returns>
        private static string ReplaceQuotationMarksInCsvData(string str)
        {
            // Remove first and last single quotation mark (enclosing quotation marks) and remove quotation marks of an empty data set ("").
            str = CsvRemoveSingleQuotationMarksRegex.Replace(str, string.Empty);

            // Remove first quotation mark if followed by pairs of quotation marks
            // and remove last quotation mark if precede by pairs of quotation marks.
            // (Removes enclosing quotation marks around the cell data for data like /"""abc"""/.)
            str = CsvRemoveStartAndEndQuotationMarksRegex.Replace(str, string.Empty);

            // Replace pairs of two quotation marks with a single quotation mark. (Escaped quotation marks.)
            str = CsvReplaceDoubleQuotationMarksRegex.Replace(str, "\"");

            return str;
        }
    }
}
