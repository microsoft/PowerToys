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
    public class LaserPointerSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "LaserPointer";

        [JsonPropertyName("properties")]
        public LaserPointerProperties Properties { get; set; }

        public LaserPointerSettings()
        {
            Name = ModuleName;
            Properties = new LaserPointerProperties();
            Version = "1.0";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.LaserPointer;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "MouseUtils_LaserPointer_ActivationShortcut"),

                // Order matters: the runner identifies hotkeys by index, so this must
                // stay aligned with HotkeyId in the module's dllmain.cpp.
                new HotkeyAccessor(
                    () => Properties.PenActivationShortcut,
                    value => Properties.PenActivationShortcut = value ?? Properties.DefaultPenActivationShortcut,
                    "MouseUtils_LaserPointer_PenActivationShortcut"),
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
