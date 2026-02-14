// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FancyZonesSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "FancyZones";

        public FancyZonesSettings()
        {
            Version = "1.0";
            Name = ModuleName;
            Properties = new FZConfigProperties();
        }

        [JsonPropertyName("properties")]
        public FZConfigProperties Properties { get; set; }

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            return [new HotkeyAccessor(() => Properties.FancyzonesEditorHotkey.Value, (hotkey) => Properties.FancyzonesEditorHotkey.Value = hotkey ?? FZConfigProperties.DefaultEditorHotkeyValue, "FancyZones_LaunchEditorButtonControl")];
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType()
        {
            return ModuleType.FancyZones;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
