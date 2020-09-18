// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public static class SettingsUtils
    {
        private const string DefaultFileName = "settings.json";
        private const string DefaultModuleName = "";

        public static void DeleteSettings(string powertoy, string fileName = DefaultFileName)
        {
            File.Delete(GetSettingsPath(powertoy, fileName));
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
            where T : ISettingsConfig, new()
        {
            if (SettingsExists(powertoy, fileName))
            {
                var deserializedSettings = GetFile<T>(powertoy, fileName);

                // If GeneralSettings is being accessed for the first time, perform a check on the version number and update accordingly.
                if (powertoy.Equals(DefaultModuleName))
                {
                    try
                    {
                        if (Helper.CompareVersions(((GeneralSettings)(object)deserializedSettings).PowertoysVersion, Helper.GetProductVersion()) < 0)
                        {
                            // Update settings
                            ((GeneralSettings)(object)deserializedSettings).PowertoysVersion = Helper.GetProductVersion();
                            SaveSettings(((GeneralSettings)(object)deserializedSettings).ToJsonString(), powertoy, fileName);
                        }
                    }
                    catch (FormatException)
                    {
                        // If there is an issue with the version number format, don't migrate settings.
                    }
                }

                return deserializedSettings;
            }
            else
            {
                T newSettingsItem = new T();
                SaveSettings(newSettingsItem.ToJsonString(), powertoy, fileName);
                return newSettingsItem;
            }
        }

        // Given the powerToy folder name and filename to be accessed, this function deserializes and returns the file.
        public static T GetFile<T>(string powertoyFolderName, string fileName)
        {
            var jsonSettingsString = File.ReadAllText(GetSettingsPath(powertoyFolderName, fileName));
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

        public static Func<string, int> DefaultSndMsgCallback { get; set; }

        public static Func<string, int> SendRestartAdminIPCMessage { get; set; }

        public static Func<string, int> SendCheckForUpdatesIPCMessage { get; set; }

        public static void SendDefaultMessageToRunner<T>(T msgToRunner)
        {
            DefaultSndMsgCallback(msgToRunner.ToString());
        }

        public static void SendRestartAsAdminMessageToRunner<T>(T msgToRunner)
        {
            SendRestartAdminIPCMessage(msgToRunner.ToString());
        }

        public static void SendCheckForUpdatesMessageToRunner<T>(T msgToRunner)
        {
            SendCheckForUpdatesIPCMessage(msgToRunner.ToString());
        }
    }
}
