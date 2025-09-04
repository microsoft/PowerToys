// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class DwellCursorSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "DwellCursor";

        [JsonPropertyName("properties")]
        public DwellCursorSettingsProperties Properties { get; set; } = new DwellCursorSettingsProperties();

        public DwellCursorSettings()
        {
            Name = ModuleName;
            Version = "1.0";
        }

        public string GetModuleName() => Name;

        public ModuleType GetModuleType() => ModuleType.MouseJump; // grouped under Mouse Utils UI

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            return new HotkeyAccessor[]
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? DwellCursorSettingsProperties.DefaultActivationShortcut,
                    "MouseUtils_DwellCursor_ActivationShortcut"),
            };
        }

        public bool UpgradeSettingsConfiguration() => false;
    }
}
