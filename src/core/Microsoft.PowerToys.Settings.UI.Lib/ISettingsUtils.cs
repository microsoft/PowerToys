// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public interface ISettingsUtils
    {
        T GetSettings<T>(string powertoy = "", string fileName = "settings.json");

        void SaveSettings(string jsonSettings, string powertoy = "", string fileName = "settings.json");

        bool SettingsExists(string powertoy = "", string fileName = "settings.json");

        void DeleteSettings(string powertoy = "");
    }
}
