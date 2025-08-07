// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Microsoft.PowerToys.Settings.UI.XamlIndexBuilder
{
    public enum EntryType
    {
        SettingsPage,
        SettingsCard,
    }

    public class Program
    {
        private static JsonSerializerOptions serializeOption = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Debug.WriteLine("Usage: XamlIndexBuilder <xaml-directory> <output-json-file>");
                Environment.Exit(1);
            }

            string xamlDirectory = args[0];
            string outputFile = args[1];

            if (!Directory.Exists(xamlDirectory))
            {
                Debug.WriteLine($"Error: Directory '{xamlDirectory}' does not exist.");
                Environment.Exit(1);
            }

            try
            {
                var searchableElements = new List<SearchableElementMetadata>();
                var xamlFiles = Directory.GetFiles(xamlDirectory, "*.xaml", SearchOption.AllDirectories);

                foreach (var xamlFile in xamlFiles)
                {
                    if (xamlFile.Equals("ShellPage.xaml", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip ShellPage.xaml as it contains many elements not relevant for search
                        continue;
                    }

                    Debug.WriteLine($"Processing: {Path.GetFileName(xamlFile)}");
                    var elements = ExtractSearchableElements(xamlFile);
                    searchableElements.AddRange(elements);
                }

                searchableElements = searchableElements.OrderBy(e => e.PageName).ThenBy(e => e.ElementName).ToList();

                string json = JsonSerializer.Serialize(searchableElements, serializeOption);
                File.WriteAllText(outputFile, json);

                Debug.WriteLine($"Successfully generated index with {searchableElements.Count} elements.");
                Debug.WriteLine($"Output written to: {outputFile}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        public static List<SearchableElementMetadata> ExtractSearchableElements(string xamlFile)
        {
            var elements = new List<SearchableElementMetadata>();
            string pageName = Path.GetFileNameWithoutExtension(xamlFile);

            try
            {
                // Load XAML as XML
                var doc = XDocument.Load(xamlFile);

                // Define namespaces
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                XNamespace controls = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                XNamespace labs = "using:CommunityToolkit.Labs.WinUI";
                XNamespace winui = "using:CommunityToolkit.WinUI.UI.Controls";

                // Extract SettingsPageControl elements
                var settingsPageElements = doc.Descendants()
                    .Where(e => e.Name.LocalName == "SettingsPageControl")
                    .Where(e => e.Attribute(x + "Uid") != null);

                // Extract SettingsCard elements
                var settingsElements = doc.Descendants()
                    .Where(e => e.Name.LocalName == "SettingsCard")
                    .Where(e => e.Attribute("Name") != null || e.Attribute(x + "Uid") != null);

                // Process SettingsPageControl elements
                foreach (var element in settingsPageElements)
                {
                    var elementUid = GetElementUid(element, x);

                    if (!string.IsNullOrEmpty(elementUid))
                    {
                        elements.Add(new SearchableElementMetadata
                        {
                            PageName = pageName,
                            Type = EntryType.SettingsPage,
                            ParentElementName = string.Empty,
                            ElementName = string.Empty,
                            ElementUid = elementUid,
                        });
                    }
                }

                // Process SettingsCard elements
                foreach (var element in settingsElements)
                {
                    var elementName = GetElementName(element, x);
                    var elementUid = GetElementUid(element, x);

                    if (!string.IsNullOrEmpty(elementName) || !string.IsNullOrEmpty(elementUid))
                    {
                        var parentElementName = GetParentElementName(element, x);

                        elements.Add(new SearchableElementMetadata
                        {
                            PageName = pageName,
                            Type = EntryType.SettingsCard,
                            ParentElementName = parentElementName,
                            ElementName = elementName,
                            ElementUid = elementUid,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing {xamlFile}: {ex.Message}");
            }

            return elements;
        }

        public static string GetElementName(XElement element, XNamespace x)
        {
            // Get Name attribute (we call it ElementName in our indexing system)
            var name = element.Attribute("Name")?.Value;
            return name;
        }

        public static string GetElementUid(XElement element, XNamespace x)
        {
            // Try x:Uid
            var uid = element.Attribute(x + "Uid")?.Value;
            return uid;
        }

        public static string GetParentElementName(XElement element, XNamespace x)
        {
            // Since expanders are no longer supported, we can return empty string
            // or implement other parent element logic if needed in the future
            return string.Empty;
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class SearchableElementMetadata
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string PageName { get; set; }

        public EntryType Type { get; set; }

        public string ParentElementName { get; set; }

        public string ElementName { get; set; }

        public string ElementUid { get; set; }
    }
}
