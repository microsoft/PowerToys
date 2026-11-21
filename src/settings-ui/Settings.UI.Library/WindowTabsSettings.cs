// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class WindowTabsSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "WindowTabs";

        [JsonPropertyName("properties")]
        public WindowTabsProperties Properties { get; set; }

        public WindowTabsSettings()
        {
            Name = ModuleName;
            Properties = new WindowTabsProperties();
            Version = "1.0";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.WindowTabs;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.NextTabShortcut,
                    value => Properties.NextTabShortcut = value ?? Properties.DefaultNextTabShortcut,
                    "WindowTabs_NextTabShortcut"),
                new HotkeyAccessor(
                    () => Properties.PreviousTabShortcut,
                    value => Properties.PreviousTabShortcut = value ?? Properties.DefaultPreviousTabShortcut,
                    "WindowTabs_PreviousTabShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
