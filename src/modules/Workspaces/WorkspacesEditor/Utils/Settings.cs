// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace WorkspacesEditor.Utils
{
    public class Settings
    {
        private const string WorkspacesModuleName = "Workspaces";
        private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;

        public static WorkspacesSettings ReadSettings()
        {
            if (!_settingsUtils.SettingsExists(WorkspacesModuleName))
            {
                WorkspacesSettings defaultWorkspacesSettings = new();
                defaultWorkspacesSettings.Save(_settingsUtils);
                return defaultWorkspacesSettings;
            }

            WorkspacesSettings settings = _settingsUtils.GetSettingsOrDefault<WorkspacesSettings>(WorkspacesModuleName);
            return settings;
        }
    }
}
