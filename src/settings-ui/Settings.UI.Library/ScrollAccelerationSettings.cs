// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ScrollAccelerationSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "ScrollAcceleration";

        public ScrollAccelerationSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new ScrollAccelerationProperties();
        }

        [JsonPropertyName("properties")]
        public ScrollAccelerationProperties Properties { get; set; }

        public string GetModuleName() => Name;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "MouseUtils_ScrollAcceleration_ActivationShortcut"),
                new HotkeyAccessor(
                    () => Properties.ExcludeAppShortcut,
                    value => Properties.ExcludeAppShortcut = value ?? Properties.DefaultExcludeAppShortcut,
                    "MouseUtils_ScrollAcceleration_ExcludeAppShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        public bool UpgradeSettingsConfiguration() => false;

        public ModuleType GetModuleType() => ModuleType.ScrollAcceleration;
    }
}
