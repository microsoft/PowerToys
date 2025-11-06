// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Helper class for configuring PowerToys settings for UI tests.
    /// </summary>
    public class SettingsConfigHelper
    {
        private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Configures global PowerToys settings to enable only specified modules and disable all others.
        /// </summary>
        /// <param name="modulesToEnable">Array of module names to enable (e.g., "Peek", "FancyZones"). All other modules will be disabled.</param>
        /// <exception cref="ArgumentNullException">Thrown when modulesToEnable is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when settings file operations fail.</exception>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This is test code and will not be trimmed")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This is test code and will not be AOT compiled")]
        public static void ConfigureGlobalModuleSettings(params string[] modulesToEnable)
        {
            ArgumentNullException.ThrowIfNull(modulesToEnable);

            try
            {
                var settingsUtils = new SettingsUtils();

                // Get settings or create default if they don't exist
                GeneralSettings settings;
                try
                {
                    settings = settingsUtils.GetSettingsOrDefault<GeneralSettings>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load settings, creating defaults: {ex.Message}");
                    settings = new GeneralSettings();
                }

                // Convert settings to JSON string
                string settingsJson = settings.ToJsonString();

                // Deserialize to JsonDocument to manipulate the Enabled modules
                using (JsonDocument doc = JsonDocument.Parse(settingsJson))
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var root = doc.RootElement.Clone();

                    // Get the enabled modules object
                    if (root.TryGetProperty("enabled", out var enabledElement))
                    {
                        // Create a dictionary of module properties with their enable states
                        var enabledModules = new Dictionary<string, bool>();

                        // Iterate through all properties in the enabled object
                        foreach (var property in enabledElement.EnumerateObject())
                        {
                            string moduleName = property.Name;

                            // Check if this module should be enabled
                            bool shouldEnable = Array.Exists(modulesToEnable, m => string.Equals(m, moduleName, StringComparison.Ordinal));
                            enabledModules[moduleName] = shouldEnable;
                        }

                        // Rebuild the settings with updated enabled modules
                        var settingsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(settingsJson);
                        if (settingsDict != null)
                        {
                            settingsDict["enabled"] = enabledModules;
                            settingsJson = JsonSerializer.Serialize(settingsDict, IndentedJsonOptions);
                        }
                    }
                }

                // Save the modified settings
                settingsUtils.SaveSettings(settingsJson);

                string enabledList = modulesToEnable.Length > 0 ? string.Join(", ", modulesToEnable) : "none";
                Debug.WriteLine($"Successfully updated global settings");
                Debug.WriteLine($"Enabled modules: {enabledList}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in ConfigureGlobalModuleSettings: {ex.Message}");
                throw new InvalidOperationException($"Failed to configure global module settings: {ex.Message}", ex);
            }
        }
    }
}
