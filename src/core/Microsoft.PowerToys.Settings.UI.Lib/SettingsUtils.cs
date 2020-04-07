using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public static class SettingsUtils
    {
        // Get path to the json settings file.
        public static string GetSettingsPath(string powertoy)
        {
            if (string.IsNullOrWhiteSpace(powertoy))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    $"Microsoft\\PowerToys\\settings.json");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"Microsoft\\PowerToys\\{powertoy}\\settings.json");
        }

        // Get a Deserialized object of the json settings string.
        public static T GetSettings<T>(string powertoy)
        {
            var jsonSettingsString = System.IO.File.ReadAllText(SettingsUtils.GetSettingsPath(powertoy));
            return JsonSerializer.Deserialize<T>(jsonSettingsString);
        }

        // Save settings to a json file.
        public static void SaveSettings(string moduleJsonSettings, string powertoyModuleName)
        {
            System.IO.File.WriteAllText(
                SettingsUtils.GetSettingsPath(powertoyModuleName),
                moduleJsonSettings);
        }
    }
}
