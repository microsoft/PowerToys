// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public interface ISettingsPath
    {
        bool SettingsFolderExists(string powertoy);

        void CreateSettingsFolder(string powertoy);

        void DeleteSettings(string powertoy = "");

        string GetSettingsPath(string powertoy, string fileName);
    }
}
