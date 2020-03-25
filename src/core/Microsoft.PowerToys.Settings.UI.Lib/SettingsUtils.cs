using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public static class SettingsUtils
    {
        /// <summary>
        /// Get path to the json settings file.
        /// </summary>
        /// <returns>string path.</returns>
        public static string GetSettingsPath(string powertoy)
        {
            if(string.IsNullOrWhiteSpace(powertoy))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    $"Microsoft\\PowerToys\\settings.json");
            }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"Microsoft\\PowerToys\\{powertoy}\\settings.json");
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

        /// <summary>
        /// Save settings to a json file.
        /// </summary>
        /// <param name="settings">dynamic json settings object.</param>
        public static void SaveSettings<T>(T settings, string powertoy)
        {
            if(settings != null)
            {
                System.IO.File.WriteAllText(
                    SettingsUtils.GetSettingsPath(powertoy),
                    settings.ToString());
            }
        }
    }
}
