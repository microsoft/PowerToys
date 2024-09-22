// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FileActionsMenuProperties : ISettingsConfig
    {
        public HotkeySettings DefaultFileActionsMenuShortcut => new(false, true, true, false, 65);

        public FileActionsMenuProperties()
        {
            FileActionsMenuShortcut = DefaultFileActionsMenuShortcut;
        }

        private bool enableFileActionsMenu = true;

        [JsonPropertyName("file-actions-menu-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableFileActionsMenu
        {
            get => enableFileActionsMenu;
            set
            {
                if (value != enableFileActionsMenu)
                {
                    enableFileActionsMenu = value;
                }
            }
        }

        [JsonPropertyName("file-actions-menu-shortcut-setting")]
        public HotkeySettings FileActionsMenuShortcut { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        // This function is required to implement the ISettingsConfig interface and obtain the settings configurations.
        public string GetModuleName()
        {
            string moduleName = FileActionsMenuSettings.ModuleName;
            return moduleName;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
