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
                string globalSettingsPath = GetGlobalSettingsPath();

                if (!File.Exists(globalSettingsPath))
                {
                    throw new InvalidOperationException($"Global settings file not found at {globalSettingsPath}");
                }

                string globalSettingsJson = File.ReadAllText(globalSettingsPath);
                using var doc = JsonDocument.Parse(globalSettingsJson);
                var root = doc.RootElement;

                // Create a dictionary to hold the modified settings
                var modifiedSettings = new Dictionary<string, object>();

                // Copy all existing properties
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "enabled")
                    {
                        // Modify the enabled property to enable only specified modules
                        var enabledModules = new Dictionary<string, bool>();
                        foreach (var module in property.Value.EnumerateObject())
                        {
                            // Set module to true if in modulesToEnable array, otherwise false
                            enabledModules[module.Name] = modulesToEnable.Contains(module.Name, StringComparer.OrdinalIgnoreCase);
                        }

                        modifiedSettings[property.Name] = enabledModules;
                    }
                    else
                    {
                        // Copy other properties as-is
                        object? deserializedValue = JsonSerializer.Deserialize<object>(property.Value.GetRawText());
                        if (deserializedValue != null)
                        {
                            modifiedSettings[property.Name] = deserializedValue;
                        }
                    }
                }

                // Serialize and save the modified settings
                string modifiedJson = JsonSerializer.Serialize(modifiedSettings, IndentedJsonOptions);
                File.WriteAllText(globalSettingsPath, modifiedJson);

                string enabledList = modulesToEnable.Length > 0 ? string.Join(", ", modulesToEnable) : "none";
                Debug.WriteLine($"Successfully updated global settings at {globalSettingsPath}");
                Debug.WriteLine($"Enabled modules: {enabledList}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in ConfigureGlobalModuleSettings: {ex.Message}");
                throw new InvalidOperationException($"Failed to configure global module settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the path to the global PowerToys settings file.
        /// </summary>
        /// <returns>Full path to the settings.json file.</returns>
        private static string GetGlobalSettingsPath()
        {
            string powerToysSettingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys");

            return Path.Combine(powerToysSettingsDirectory, "settings.json");
        }
    }
}
