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
    public class MeasureToolSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "Measure Tool";

        [JsonPropertyName("properties")]
        public MeasureToolProperties Properties { get; set; }

        public MeasureToolSettings()
        {
            Properties = new MeasureToolProperties();
            Version = "1";
            Name = ModuleName;
        }

        public string GetModuleName()
            => Name;

        public ModuleType GetModuleType() => ModuleType.MeasureTool;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "MeasureTool_ActivationShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
            => false;
    }
}
