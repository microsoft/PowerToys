// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public static class SettingsUtils
    {
        private const string DefaultFileName = "settings.json";
        private const string DefaultModuleName = "";

        public static bool SettingsFolderExists(string powertoy)
        {
            return Directory.Exists(Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        public static void CreateSettingsFolder(string powertoy)
        {
            Directory.CreateDirectory(Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        /// <summary>
        /// Get path to the json settings file.
        /// </summary>
        /// <returns>string path.</returns>
        public static string GetSettingsPath(string powertoy, string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(powertoy))
            {
                return Path.Combine(
                    LocalApplicationDataFolder(),
                    $"Microsoft\\PowerToys\\{fileName}");
            }

            return Path.Combine(
                LocalApplicationDataFolder(),
                $"Microsoft\\PowerToys\\{powertoy}\\{fileName}");
        }

        public static bool SettingsExists(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            return File.Exists(GetSettingsPath(powertoy, fileName));
        }

        /// <summary>
        /// Get a Deserialized object of the json settings string.
        /// </summary>
        /// <returns>Deserialized json settings object.</returns>
        public static T GetSettings<T>(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            var jsonSettingsString = File.ReadAllText(GetSettingsPath(powertoy, fileName));
            return JsonSerializer.Deserialize<T>(jsonSettingsString);
        }

        // Save settings to a json file.
        public static void SaveSettings(string jsonSettings, string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            try
            {
                if (jsonSettings != null)
                {
                    if (!SettingsFolderExists(powertoy))
                    {
                        CreateSettingsFolder(powertoy);
                    }

                    File.WriteAllText(GetSettingsPath(powertoy, fileName), jsonSettings);
                }
            }
            catch
            {
            }
        }

        public static string LocalApplicationDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
    }
}
