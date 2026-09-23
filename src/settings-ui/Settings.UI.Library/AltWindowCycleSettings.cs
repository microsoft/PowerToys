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
    public class AltWindowCycleSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "AltWindowCycle";

        [JsonPropertyName("properties")]
        public AltWindowCycleProperties Properties { get; set; }

        public AltWindowCycleSettings()
        {
            Name = ModuleName;
            Properties = new AltWindowCycleProperties();
            Version = "1.0";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.AltWindowCycle;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.NextWindowShortcut,
                    value => Properties.NextWindowShortcut = value ?? Properties.DefaultNextWindowShortcut,
                    "AltWindowCycle_NextWindowShortcut"),
                new HotkeyAccessor(
                    () => Properties.PreviousWindowShortcut,
                    value => Properties.PreviousWindowShortcut = value ?? Properties.DefaultPreviousWindowShortcut,
                    "AltWindowCycle_PreviousWindowShortcut"),
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
