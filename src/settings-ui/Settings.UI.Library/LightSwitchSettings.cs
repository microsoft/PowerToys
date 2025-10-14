// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Settings.UI.Library
{
    public class LightSwitchSettings : BasePTModuleSettings, ISettingsConfig, ICloneable
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
