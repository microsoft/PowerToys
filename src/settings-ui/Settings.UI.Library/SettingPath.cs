// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SettingPath : ISettingsPath
    {
        private const string DefaultFileName = "settings.json";

        private readonly IDirectory _directory;

        private readonly IPath _path;

        public SettingPath(IDirectory directory, IPath path)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public bool SettingsFolderExists(string powertoy)
        {
            return _directory.Exists(System.IO.Path.Combine(Helper.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        public void CreateSettingsFolder(string powertoy)
        {
            _directory.CreateDirectory(System.IO.Path.Combine(Helper.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        public void DeleteSettings(string powertoy = "")
        {
            _directory.Delete(System.IO.Path.Combine(Helper.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        /// <summary>
        /// Get path to the json settings file.
        /// </summary>
        /// <returns>string path.</returns>
        public string GetSettingsPath(string powertoy, string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(powertoy))
            {
                return _path.Combine(
                    Helper.LocalApplicationDataFolder(),
                    $"Microsoft\\PowerToys\\{fileName}");
            }

            return _path.Combine(
                Helper.LocalApplicationDataFolder(),
                $"Microsoft\\PowerToys\\{powertoy}\\{fileName}");
        }
    }
}
