// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.PowerToys.Settings.UI.XamlIndexBuilder
{
    public static class ModuleIconResolver
    {
        // Hardcoded page-level overrides for module -> icon path
        private static readonly System.Collections.Generic.Dictionary<string, string> FileNameOverrides = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Example overrides; expand as needed
            { "FancyZonesPage.xaml", "/Assets/Settings/Icons/FancyZones.png" },
            { "FileLocksmithPage.xaml", "/Assets/Settings/Icons/FileLocksmith.png" },
            { "CmdNotFoundPage.xaml", "/Assets/Settings/Icons/CommandNotFound.png" },
            { "PowerLauncherPage.xaml", "/Assets/Settings/Icons/PowerToysRun.png" },
        };

        // Contract:
        // - Input: absolute path to the module XAML file (e.g., FancyZonesPage.xaml)
        // - Output: app-relative icon path (e.g., "/Assets/Settings/Icons/FancyZones.png"), or null if not found
        // - Strategy: take the first SettingsCard under the page and read its HeaderIcon value
        public static string ResolveIconFromFirstSettingsCard(string xamlFilePath)
        {
            if (string.IsNullOrWhiteSpace(xamlFilePath))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Load(xamlFilePath);

                // Prefer looking inside SettingsPageControl.ModuleContent to avoid picking cards in Resources/DataTemplates
                var pageControl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "SettingsPageControl");

                if (pageControl != null)
                {
                    // Locate the property element <SettingsPageControl.ModuleContent>
                    var moduleContent = pageControl
                        .Elements()
                        .FirstOrDefault(e => e.Name.LocalName.EndsWith(".ModuleContent", System.StringComparison.OrdinalIgnoreCase))
                        ?? pageControl
                            .Descendants()
                            .FirstOrDefault(e => e.Name.LocalName.EndsWith(".ModuleContent", System.StringComparison.OrdinalIgnoreCase));

                    if (moduleContent != null)
                    {
                        // Find the first SettingsCard under ModuleContent and try to read its HeaderIcon
                        var firstCardUnderModule = moduleContent
                            .Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "SettingsCard");

                        if (firstCardUnderModule != null)
                        {
                            var icon = Program.ExtractIconValue(firstCardUnderModule);
                            if (!string.IsNullOrWhiteSpace(icon))
                            {
                                return icon;
                            }
                        }
                    }
                }

                // Fallback to hardcoded overrides by file name
                var fileName = Path.GetFileName(xamlFilePath);
                if (!string.IsNullOrEmpty(fileName) && FileNameOverrides.TryGetValue(fileName, out var overrideIcon))
                {
                    return overrideIcon;
                }

                return null;
            }
            catch
            {
                // Non-fatal: let caller decide fallback
                return null;
            }
        }
    }
}
