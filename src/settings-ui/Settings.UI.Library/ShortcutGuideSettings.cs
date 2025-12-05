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
    public class ShortcutGuideSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "Shortcut Guide";

        [JsonPropertyName("properties")]
        public ShortcutGuideProperties Properties { get; set; }

        public ShortcutGuideSettings()
        {
            Name = ModuleName;
            Properties = new ShortcutGuideProperties();
            Version = "1.0";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.ShortcutGuide;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.OpenShortcutGuide,
                    value => Properties.OpenShortcutGuide = value ?? Properties.DefaultOpenShortcutGuide,
                    "Activation_Shortcut"),
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
