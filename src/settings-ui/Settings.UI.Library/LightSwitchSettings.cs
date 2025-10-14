// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Settings.UI.Library
{
    public class LightSwitchSettings : BasePTModuleSettings, ISettingsConfig, ICloneable, IHotkeyConfig
    {
        public const string ModuleName = "LightSwitch";

        public LightSwitchSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new LightSwitchProperties();
        }

        [JsonPropertyName("properties")]
        public LightSwitchProperties Properties { get; set; }

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ToggleThemeHotkey.Value,
                    value => Properties.ToggleThemeHotkey.Value = value ?? LightSwitchProperties.DefaultToggleThemeHotkey,
                    "LightSwitch_ThemeToggle_Shortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        public ModuleType GetModuleType() => ModuleType.LightSwitch;

        public object Clone()
        {
            return new LightSwitchSettings()
            {
                Name = Name,
                Version = Version,
                Properties = new LightSwitchProperties()
                {
                    ChangeSystem = Properties.ChangeSystem,
                    ChangeApps = Properties.ChangeApps,
                    ScheduleMode = Properties.ScheduleMode,
                    LightTime = Properties.LightTime,
                    DarkTime = Properties.DarkTime,
                    SunriseOffset = Properties.SunriseOffset,
                    SunsetOffset = Properties.SunsetOffset,
                    Latitude = Properties.Latitude,
                    Longitude = Properties.Longitude,
                    ToggleThemeHotkey = Properties.ToggleThemeHotkey,
                },
            };
        }

        public string GetModuleName()
        {
            return Name;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
