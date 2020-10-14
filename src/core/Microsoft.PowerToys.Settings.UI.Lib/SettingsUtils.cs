// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class SettingsUtils : ISettingsUtils
    {
        private const string DefaultFileName = "settings.json";
        private const string DefaultModuleName = "";
        private readonly IFile _file;
        private readonly ISettingsPath _settingsPath;

        public SettingsUtils()
            : this(new FileSystem())
        {
        }

        public SettingsUtils(IFileSystem fileSystem)
            : this(fileSystem.File, new SettingPath(fileSystem.Directory, fileSystem.Path))
        {
        }

        public SettingsUtils(IFile file, ISettingsPath settingPath)
        {
            _file = file;
            _settingsPath = settingPath;
        }

        public bool SettingsExists(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            var settingsPath = _settingsPath.GetSettingsPath(powertoy, fileName);
            return _file.Exists(settingsPath);
        }

        public void DeleteSettings(string powertoy = "")
        {
            _settingsPath.DeleteSettings(powertoy);
        }

        /// <summary>
        /// Get a Deserialized object of the json settings string.
        /// This function creates a file in the powertoy folder if it does not exist and returns an object with default properties.
        /// </summary>
        /// <returns>Deserialized json settings object.</returns>
        public T GetSettings<T>(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
            where T : ISettingsConfig, new()
        {
            if (SettingsExists(powertoy, fileName))
            {
                // Given the file already exists, to deserialize the file and read it's content.
                T deserializedSettings = GetFile<T>(powertoy, fileName);

                // IF the file needs to be modified, to save the new configurations accordingly.
                if (deserializedSettings.UpgradeSettingsConfiguration())
                {
                    SaveSettings(deserializedSettings.ToJsonString(), powertoy, fileName);
                }

                return deserializedSettings;
            }
            else
            {
                // If the settings file does not exist, to create a new object with default parameters and save it to a newly created settings file.
                T newSettingsItem = new T();
                SaveSettings(newSettingsItem.ToJsonString(), powertoy, fileName);
                return newSettingsItem;
            }
        }

        // Given the powerToy folder name and filename to be accessed, this function deserializes and returns the file.
        private T GetFile<T>(string powertoyFolderName = DefaultModuleName, string fileName = DefaultFileName)
        {
            // Adding Trim('\0') to overcome possible NTFS file corruption.
            // Look at issue https://github.com/microsoft/PowerToys/issues/6413 you'll see the file has a large sum of \0 to fill up a 4096 byte buffer for writing to disk
            // This, while not totally ideal, does work around the problem by trimming the end.
            // The file itself did write the content correctly but something is off with the actual end of the file, hence the 0x00 bug
            var jsonSettingsString = _file.ReadAllText(_settingsPath.GetSettingsPath(powertoyFolderName, fileName)).Trim('\0');
            return JsonSerializer.Deserialize<T>(jsonSettingsString);
        }

        // Save settings to a json file.
        public void SaveSettings(string jsonSettings, string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            try
            {
                if (jsonSettings != null)
                {
                    if (!_settingsPath.SettingsFolderExists(powertoy))
                    {
                        _settingsPath.CreateSettingsFolder(powertoy);
                    }

                    _file.WriteAllText(_settingsPath.GetSettingsPath(powertoy, fileName), jsonSettings);
                }
            }
            catch
            {
            }
        }
    }
}
