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

                // Sort by page name for consistent output
                searchableElements = searchableElements.OrderBy(e => e.PageName).ThenBy(e => e.AutomationId).ToList();

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

                // Extract SettingsCards
                var settingsCards = doc.Descendants()
                    .Where(e => e.Name.LocalName == "SettingsCard" ||
                               e.Name.LocalName == "SettingsExpander" ||
                               e.Name.LocalName == "SettingsGroup")
                    .Where(e => e.Attribute(x + "Name") != null || e.Attribute("AutomationProperties.AutomationId") != null);

                foreach (var card in settingsCards)
                {
                    var automationId = GetAutomationId(card, x);
                    if (!string.IsNullOrEmpty(automationId))
                    {
                        elements.Add(new SearchableElementMetadata
                        {
                            PageName = pageName,
                            AutomationId = automationId,
                            ControlType = card.Name.LocalName,
                        });
                    }
                }

                // Extract Buttons, CheckBoxes, RadioButtons, ToggleButtons, ToggleSwitches
                var interactiveControls = doc.Descendants()
                    .Where(e => e.Name.LocalName == "Button" ||
                               e.Name.LocalName == "CheckBox" ||
                               e.Name.LocalName == "RadioButton" ||
                               e.Name.LocalName == "ToggleButton" ||
                               e.Name.LocalName == "ToggleSwitch")
                    .Where(e => e.Attribute(x + "Name") != null || e.Attribute("AutomationProperties.AutomationId") != null);

                foreach (var control in interactiveControls)
                {
                    var automationId = GetAutomationId(control, x);
                    if (!string.IsNullOrEmpty(automationId))
                    {
                        elements.Add(new SearchableElementMetadata
                        {
                            PageName = pageName,
                            AutomationId = automationId,
                            ControlType = control.Name.LocalName,
                        });
                    }
                }

                // Extract TextBlocks with x:Uid for section headers
                var textBlocks = doc.Descendants()
                    .Where(e => e.Name.LocalName == "TextBlock")
                    .Where(e => e.Attribute(x + "Uid") != null);

                foreach (var textBlock in textBlocks)
                {
                    var uid = textBlock.Attribute(x + "Uid")?.Value;
                    if (!string.IsNullOrEmpty(uid) && IsLikelyHeader(textBlock))
                    {
                        elements.Add(new SearchableElementMetadata
                        {
                            PageName = pageName,
                            AutomationId = uid,
                            ControlType = "TextBlock",
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

        public static string GetAutomationId(XElement element, XNamespace x)
        {
            // First try AutomationProperties.AutomationId
            var automationId = element.Attribute("AutomationProperties.AutomationId")?.Value;
            if (!string.IsNullOrEmpty(automationId))
            {
                return automationId;
            }

            // Then try x:Name
            var name = element.Attribute(x + "Name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            // Finally try x:Uid
            var uid = element.Attribute(x + "Uid")?.Value;
            return uid;
        }

        public static bool IsLikelyHeader(XElement textBlock)
        {
            // Check if TextBlock has styling that suggests it's a header
            var style = textBlock.Attribute("Style")?.Value;
            if (!string.IsNullOrEmpty(style) &&
                (style.Contains("Subtitle") || style.Contains("Title") || style.Contains("Header")))
            {
                return true;
            }

            // Check parent element - headers are often in specific containers
            var parent = textBlock.Parent;
            if (parent != null && (parent.Name.LocalName == "StackPanel" || parent.Name.LocalName == "Grid"))
            {
                // Check if it's the first child (often headers come first)
                var firstChild = parent.Elements().FirstOrDefault();
                if (firstChild == textBlock)
                {
                    return true;
                }
            }

            return false;
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class SearchableElementMetadata
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string PageName { get; set; }

        public string AutomationId { get; set; }

        public string ControlType { get; set; }
    }
}
