// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace JlordConsoleTester
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var backupRetoreSettings = JObject.Parse(File.ReadAllText(@"C:\Users\jeff\AppData\Local\Microsoft\PowerToys\backup-restore_settings.json"));

            var exportVersion = GetExportVersion(backupRetoreSettings, "settings.json", "C:\\Users\\jeff\\AppData\\Local\\Microsoft\\PowerToys\\settings.json");
        }

        public static JObject GetExportVersion(JObject backupRetoreSettings, string settingFileKey, string settingsFileName)
        {
            var ignoredSettings = GetIgnoredSettings(backupRetoreSettings, settingFileKey);
            var settingsFile = JObject.Parse(File.ReadAllText(settingsFileName));

            if (ignoredSettings.Length == 0)
            {
                return settingsFile;
            }

            foreach (var property in settingsFile.Properties().ToList())
            {
                if (ignoredSettings.Contains(property.Name))
                {
                    settingsFile.Remove(property.Name);
                }
            }

            return settingsFile;
        }

        private static string[] GetIgnoredSettings(JObject backupRetoreSettings, string settingFileKey)
        {
            if (backupRetoreSettings == null)
            {
                throw new ArgumentNullException(nameof(backupRetoreSettings));
            }

            var ignoredSettings = backupRetoreSettings["IgnoredSettings"];

            if (ignoredSettings != null && ignoredSettings[settingFileKey] != null)
            {
                ignoredSettings = ignoredSettings[settingFileKey];
                if (ignoredSettings != null)
                {
                    return ((JArray)ignoredSettings).ToObject<string[]>();
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
            else
            {
                return Array.Empty<string>();
            }
        }
    }
}
