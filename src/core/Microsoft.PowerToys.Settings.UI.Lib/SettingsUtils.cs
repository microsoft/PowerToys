using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public static class SettingsUtils
    {
        private static string LocalApplicationDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

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
        public static string GetSettingsPath(string powertoy)
        {
            if (string.IsNullOrWhiteSpace(powertoy))
            {
                return Path.Combine(
                    LocalApplicationDataFolder(),
                    $"Microsoft\\PowerToys\\settings.json");
            }

            return Path.Combine(
                LocalApplicationDataFolder(),
                $"Microsoft\\PowerToys\\{powertoy}\\settings.json");
        }

        public static bool SettingsExists(string powertoy)
        {
            return File.Exists(SettingsUtils.GetSettingsPath(powertoy));
        }

        /// <summary>
        /// Get a Deserialized object of the json settings string.
        /// </summary>
        /// <returns>Deserialized json settings object.</returns>
        public static T GetSettings<T>(string powertoy)
        {
            var jsonSettingsString = System.IO.File.ReadAllText(SettingsUtils.GetSettingsPath(powertoy));
            return JsonSerializer.Deserialize<T>(jsonSettingsString);
        }

        // Save settings to a json file.
        public static void SaveSettings(string jsonSettings, string powertoy)
        {
            if(jsonSettings != null)
            {
                if (!SettingsFolderExists(powertoy))
                {
                    CreateSettingsFolder(powertoy);
                }
                System.IO.File.WriteAllText(
                    SettingsUtils.GetSettingsPath(powertoy),
                    jsonSettings);
            }
        }
    }
}
