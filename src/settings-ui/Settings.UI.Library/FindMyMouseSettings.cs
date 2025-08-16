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
    public class FindMyMouseSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "FindMyMouse";

        [JsonPropertyName("properties")]
        public FindMyMouseProperties Properties { get; set; }

        public FindMyMouseSettings()
        {
            Name = ModuleName;
            Properties = new FindMyMouseProperties();
            Version = "1.1";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.FindMyMouse;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "MouseUtils_FindMyMouse_ActivationShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            if (Version == "1.0")
            {
                if (Properties.ActivationMethod.Value == 1)
                {
                    Properties.ActivationMethod = new IntProperty(2);
                }

                Version = "1.1";
                return true;
            }

            return false;
        }
    }
}
