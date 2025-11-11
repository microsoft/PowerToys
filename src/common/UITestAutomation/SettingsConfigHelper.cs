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
        private static readonly SettingsUtils SettingsUtils = new SettingsUtils();

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
                GeneralSettings settings;
                try
                {
                    settings = SettingsUtils.GetSettingsOrDefault<GeneralSettings>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load settings, creating defaults: {ex.Message}");
                    settings = new GeneralSettings();
                }

                string settingsJson = settings.ToJsonString();
                using (JsonDocument doc = JsonDocument.Parse(settingsJson))
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var root = doc.RootElement.Clone();

                    if (root.TryGetProperty("enabled", out var enabledElement))
                    {
                        var enabledModules = new Dictionary<string, bool>();

                        foreach (var property in enabledElement.EnumerateObject())
                        {
                            string moduleName = property.Name;

                            bool shouldEnable = Array.Exists(modulesToEnable, m => string.Equals(m, moduleName, StringComparison.Ordinal));
                            enabledModules[moduleName] = shouldEnable;
                        }

                        var settingsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(settingsJson);
                        if (settingsDict != null)
                        {
                            settingsDict["enabled"] = enabledModules;
                            settingsJson = JsonSerializer.Serialize(settingsDict, IndentedJsonOptions);
                        }
                    }
                }

                SettingsUtils.SaveSettings(settingsJson);

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

        /// <summary>
        /// Updates a module's settings file. If the file doesn't exist, creates it with default content.
        /// If the file exists, reads it and applies the provided update function to modify the settings.
        /// </summary>
        /// <param name="moduleName">The name of the module (e.g., "Peek", "FancyZones").</param>
        /// <param name="defaultSettingsContent">The default JSON content to use if the settings file doesn't exist.</param>
        /// <param name="updateSettingsAction">
        /// A callback function that modifies the settings dictionary. The function receives the deserialized settings
        /// and should modify it in-place. The function should accept a Dictionary&lt;string, object&gt; and not return a value.
        /// Example: (settings) => { ((Dictionary&lt;string, object&gt;)settings["properties"])["SomeSetting"] = newValue; }
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when moduleName or updateSettingsAction is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when settings file operations fail.</exception>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This is test code and will not be trimmed")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This is test code and will not be AOT compiled")]
        public static void UpdateModuleSettings(
            string moduleName,
            string defaultSettingsContent,
            Action<Dictionary<string, object>> updateSettingsAction)
        {
            ArgumentNullException.ThrowIfNull(moduleName);
            ArgumentNullException.ThrowIfNull(updateSettingsAction);

            try
            {
                // Build the path to the module settings file
                string powerToysSettingsDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys");

                string moduleDirectory = Path.Combine(powerToysSettingsDirectory, moduleName);
                string settingsPath = Path.Combine(moduleDirectory, "settings.json");

                // Ensure directory exists
                Directory.CreateDirectory(moduleDirectory);

                // Read existing settings or use default
                string existingJson = string.Empty;
                if (File.Exists(settingsPath))
                {
                    existingJson = File.ReadAllText(settingsPath);
                }

                Dictionary<string, object>? settings;

                // If file doesn't exist or is empty, create from defaults
                if (string.IsNullOrWhiteSpace(existingJson))
                {
                    if (string.IsNullOrWhiteSpace(defaultSettingsContent))
                    {
                        throw new ArgumentException("Default settings content must be provided when file doesn't exist.", nameof(defaultSettingsContent));
                    }

                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(defaultSettingsContent)
                               ?? throw new InvalidOperationException($"Failed to deserialize default settings for {moduleName}");

                    Debug.WriteLine($"Created default settings for {moduleName} at {settingsPath}");
                }
                else
                {
                    // Parse existing settings
                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson)
                               ?? throw new InvalidOperationException($"Failed to deserialize existing settings for {moduleName}");

                    Debug.WriteLine($"Loaded existing settings for {moduleName} from {settingsPath}");
                }

                // Apply the update action to modify settings
                updateSettingsAction(settings);

                // Serialize and save the updated settings using SettingsUtils
                string updatedJson = JsonSerializer.Serialize(settings, IndentedJsonOptions);
                SettingsUtils.SaveSettings(updatedJson, moduleName);

                Debug.WriteLine($"Successfully updated settings for {moduleName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in UpdateModuleSettings for {moduleName}: {ex.Message}");
                throw new InvalidOperationException($"Failed to update settings for {moduleName}: {ex.Message}", ex);
            }
        }
    }
}
