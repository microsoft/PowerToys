// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using ManagedCommon;
using Microsoft.Extensions.Azure;
using Newtonsoft.Json;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers
{
    internal static class JsonHelper
    {
        // Ini parts regex
        private static readonly Regex IniSectionNameRegex = new Regex("^\\[(.+)\\]");
        private static readonly Regex IniValueLineRegex = new Regex("(.+?)\\s*=\\s*(.*)");

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

            // Try convert Ini
            // (Must come before CSV that ini is not false detected as CSV.)
            try
            {
                if (string.IsNullOrEmpty(jsonText))
                {
                    var ini = new Dictionary<string, Dictionary<string, string>>();
                    var lastSectionName = string.Empty;

                    string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    // Validate content as ini (First line is a section name and second line is a key-value-pair.)
                    if (lines.Length >= 2 && IniSectionNameRegex.IsMatch(lines[0]) && IniValueLineRegex.IsMatch(lines[1]))
                    {
                        // Parse and convert Ini
                        foreach (string line in lines)
                        {
                            Match lineSectionNameCheck = IniSectionNameRegex.Match(line);
                            Match lineKeyValuePairCheck = IniValueLineRegex.Match(line);

                            if (lineSectionNameCheck.Success)
                            {
                                // Section name
                                lastSectionName = lineSectionNameCheck.Groups[0].Value;
                                ini.Add(lastSectionName, new Dictionary<string, string>());
                            }
                            else if (string.IsNullOrEmpty(lastSectionName) || !lineKeyValuePairCheck.Success)
                            {
                                // Fail if lastSectionName is still empty and not key-value-pair.
                                // (With empty lastSectionName we can't parse key-value-pairs
                                //  and if it is not a key-value-pair then the line is invalid.)
                                throw new FormatException("Invalid ini file format.");
                            }
                            else
                            {
                                // Key-value-pair
                                ini[lastSectionName].Add(lineKeyValuePairCheck.Groups[0].Value, lineKeyValuePairCheck.Groups[1].Value);
                            }
                        }

                        // Convert to JSON
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

                    foreach (var line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        csv.Add(line.Split(","));
                    }

                    jsonText = JsonConvert.SerializeObject(csv, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as csv", ex);
            }

            return string.IsNullOrEmpty(jsonText) ? text : jsonText;
        }
    }
}
