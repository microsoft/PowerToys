// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public interface ISettingsUtils
    {
        public const string DefaultFileName = "settings.json";

        T GetSettings<T>(string powertoy = "", string fileName = DefaultFileName)
            where T : ISettingsConfig, new();

        T GetSettingsOrDefault<T>(string powertoy = "", string fileName = DefaultFileName)
            where T : ISettingsConfig, new();

        void SaveSettings(string jsonSettings, string powertoy = "", string fileName = DefaultFileName);

        bool SettingsExists(string powertoy = "", string fileName = DefaultFileName);

        void DeleteSettings(string powertoy = "");

        string GetSettingsFilePath(string powertoy = "", string fileName = DefaultFileName);

        T GetSettingsOrDefault<T, T2>(string powertoy = "", string fileName = DefaultFileName, Func<object, object> settingsUpgrader = null)
            where T : ISettingsConfig, new()
            where T2 : ISettingsConfig, new();
    }
}
