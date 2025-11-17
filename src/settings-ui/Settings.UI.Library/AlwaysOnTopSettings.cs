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
    public class AlwaysOnTopSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "AlwaysOnTop";
        public const string ModuleVersion = "0.0.1";

        public AlwaysOnTopSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new AlwaysOnTopProperties();
        }

        [JsonPropertyName("properties")]
        public AlwaysOnTopProperties Properties { get; set; }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.AlwaysOnTop;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.Hotkey.Value,
                    value => Properties.Hotkey.Value = value ?? AlwaysOnTopProperties.DefaultHotkeyValue,
                    "AlwaysOnTop_ActivationShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
