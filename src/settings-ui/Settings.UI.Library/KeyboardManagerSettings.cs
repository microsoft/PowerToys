// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeyboardManagerSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Keyboard Manager";

        [JsonPropertyName("properties")]
        public KeyboardManagerProperties Properties { get; set; }

        public KeyboardManagerSettings()
        {
            Properties = new KeyboardManagerProperties();
            Version = "1";
            Name = ModuleName;
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ToggleShortcut,
                    value => Properties.ToggleShortcut = value ?? Properties.DefaultToggleShortcut,
                    "Toggle_Shortcut"),
            };

            return hotkeyAccessors.ToArray();
        }
    }
}
