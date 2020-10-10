// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public interface ISettingsUtils
    {
        T GetSettings<T>(string powertoy = "", string fileName = "settings.json")
            where T : class, ISettingsConfig, new();

        void SaveSettings<T>(T settingsObject, string powertoy = "", string fileName = "settings.json")
            where T : class, new();

        bool SettingsExists(string powertoy = "", string fileName = "settings.json");

        void DeleteSettings(string powertoy = "");
    }
}
