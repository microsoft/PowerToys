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

namespace Microsoft.PowerToys.Tools.XamlIndexBuilder
{
    public class Program
    {
        private static readonly HashSet<string> ExcludedXamlFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "ShellPage.xaml",
        };

        private static JsonSerializerOptions serializeOption = new()
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
                var searchableElements = new List<SettingEntry>();
                var xamlFiles = Directory.GetFiles(xamlDirectory, "*.xaml", SearchOption.AllDirectories);

                foreach (var xamlFile in xamlFiles)
                {
                    var fileName = Path.GetFileName(xamlFile);
                    if (ExcludedXamlFiles.Contains(fileName))
                    {
                        // Skip ShellPage.xaml as it contains many elements not relevant for search
                        continue;
                    }

                    Debug.WriteLine($"Processing: {fileName}");
                    var elements = ExtractSearchableElements(xamlFile);
                    searchableElements.AddRange(elements);
                }

                searchableElements = searchableElements.OrderBy(e => e.PageTypeName).ThenBy(e => e.ElementName).ToList();

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

        public static List<SettingEntry> ExtractSearchableElements(string xamlFile)
        {
            var elements = new List<SettingEntry>();
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

                // Extract SettingsExpander elements
                var settingsExpanderElements = doc.Descendants()
                    .Where(e => e.Name.LocalName == "SettingsExpander")
                    .Where(e => e.Attribute("Name") != null || e.Attribute(x + "Uid") != null);

                // Process SettingsPageControl elements
                foreach (var element in settingsPageElements)
                {
                    var elementUid = GetElementUid(element, x);

                    // Prefer the first SettingsCard.HeaderIcon as the module icon
                    var moduleImageSource = ModuleIconResolver.ResolveIconFromFirstSettingsCard(xamlFile);

                    if (!string.IsNullOrEmpty(elementUid))
                    {
                        elements.Add(new SettingEntry
                        {
                            PageTypeName = pageName,
                            Type = EntryType.SettingsPage,
                            ParentElementName = string.Empty,
                            ElementName = string.Empty,
                            ElementUid = elementUid,
                            Icon = moduleImageSource,
                        });
                    }
                }

                // Process SettingsCard elements
                foreach (var element in settingsElements)
                {
                    var elementName = GetElementName(element, x);
                    var elementUid = GetElementUid(element, x);
                    var headerIcon = ExtractIconValue(element);

                    if (!string.IsNullOrEmpty(elementName) || !string.IsNullOrEmpty(elementUid))
                    {
                        var parentElementName = GetParentElementName(element, x);

                        elements.Add(new SettingEntry
                        {
                            PageTypeName = pageName,
                            Type = EntryType.SettingsCard,
                            ParentElementName = parentElementName,
                            ElementName = elementName,
                            ElementUid = elementUid,
                            Icon = headerIcon,
                        });
                    }
                }

                // Process SettingsExpander elements
                foreach (var element in settingsExpanderElements)
                {
                    var elementName = GetElementName(element, x);
                    var elementUid = GetElementUid(element, x);
                    var headerIcon = ExtractIconValue(element);

                    if (!string.IsNullOrEmpty(elementName) || !string.IsNullOrEmpty(elementUid))
                    {
                        var parentElementName = GetParentElementName(element, x);

                        elements.Add(new SettingEntry
                        {
                            PageTypeName = pageName,
                            Type = EntryType.SettingsExpander,
                            ParentElementName = parentElementName,
                            ElementName = elementName,
                            ElementUid = elementUid,
                            Icon = headerIcon,
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
            // Look for parent SettingsExpander
            var current = element.Parent;
            while (current != null)
            {
                // Check if we're inside a SettingsExpander.Items or just directly inside SettingsExpander
                if (current.Name.LocalName == "Items")
                {
                    // Check if the parent of Items is SettingsExpander
                    var expanderParent = current.Parent;
                    if (expanderParent?.Name.LocalName == "SettingsExpander")
                    {
                        var expanderName = expanderParent.Attribute("Name")?.Value;
                        if (!string.IsNullOrEmpty(expanderName))
                        {
                            return expanderName;
                        }
                    }
                }
                else if (current.Name.LocalName == "SettingsExpander")
                {
                    // Direct child of SettingsExpander
                    var expanderName = current.Attribute("Name")?.Value;
                    if (!string.IsNullOrEmpty(expanderName))
                    {
                        return expanderName;
                    }
                }

                current = current.Parent;
            }

            return string.Empty;
        }

        public static string ExtractIconValue(XElement element)
        {
            var headerIconAttribute = element.Attribute("HeaderIcon")?.Value;

            if (string.IsNullOrEmpty(headerIconAttribute))
            {
                // Try nested property element: <SettingsCard.HeaderIcon> ... </SettingsCard.HeaderIcon>
                var headerIconProperty = element.Elements()
                    .FirstOrDefault(e => e.Name.LocalName.EndsWith(".HeaderIcon", StringComparison.OrdinalIgnoreCase));

                if (headerIconProperty != null)
                {
                    // Prefer explicit icon elements within the HeaderIcon property
                    var pathIcon = headerIconProperty.Descendants().FirstOrDefault(d => d.Name.LocalName == "PathIcon");
                    if (pathIcon != null)
                    {
                        var dataAttr = pathIcon.Attribute("Data")?.Value;
                        if (!string.IsNullOrWhiteSpace(dataAttr))
                        {
                            return dataAttr.Trim();
                        }
                    }

                    var fontIcon = headerIconProperty.Descendants().FirstOrDefault(d => d.Name.LocalName == "FontIcon");
                    if (fontIcon != null)
                    {
                        var glyphAttr = fontIcon.Attribute("Glyph")?.Value;
                        if (!string.IsNullOrWhiteSpace(glyphAttr))
                        {
                            return glyphAttr.Trim();
                        }
                    }

                    var bitmapIcon = headerIconProperty.Descendants().FirstOrDefault(d => d.Name.LocalName == "BitmapIcon");
                    if (bitmapIcon != null)
                    {
                        var sourceAttr = bitmapIcon.Attribute("Source")?.Value;
                        if (!string.IsNullOrWhiteSpace(sourceAttr))
                        {
                            return sourceAttr.Trim();
                        }
                    }
                }

                return null;
            }

            // Parse different icon markup extensions
            // Example: {ui:BitmapIcon Source=/Assets/Settings/Icons/AlwaysOnTop.png}
            if (headerIconAttribute.Contains("BitmapIcon") && headerIconAttribute.Contains("Source="))
            {
                var sourceStart = headerIconAttribute.IndexOf("Source=", StringComparison.OrdinalIgnoreCase) + "Source=".Length;
                var sourceEnd = headerIconAttribute.IndexOf('}', sourceStart);
                if (sourceEnd == -1)
                {
                    sourceEnd = headerIconAttribute.Length;
                }

                return headerIconAttribute.Substring(sourceStart, sourceEnd - sourceStart).Trim();
            }

            // Example: {ui:FontIcon Glyph=&#xEDA7;}
            if (headerIconAttribute.Contains("FontIcon") && headerIconAttribute.Contains("Glyph="))
            {
                var glyphStart = headerIconAttribute.IndexOf("Glyph=", StringComparison.OrdinalIgnoreCase) + "Glyph=".Length;
                var glyphEnd = headerIconAttribute.IndexOf('}', glyphStart);
                if (glyphEnd == -1)
                {
                    glyphEnd = headerIconAttribute.Length;
                }

                return headerIconAttribute.Substring(glyphStart, glyphEnd - glyphStart).Trim();
            }

            // If it doesn't match known patterns, return the original value
            return headerIconAttribute;
        }
    }
}
