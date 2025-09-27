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
                    ChangeSystem = new BoolProperty(Properties.ChangeSystem.Value),
                    ChangeApps = new BoolProperty(Properties.ChangeApps.Value),
                    ScheduleMode = new StringProperty(Properties.ScheduleMode.Value),
                    LightTime = new IntProperty((int)Properties.LightTime.Value),
                    DarkTime = new IntProperty((int)Properties.DarkTime.Value),
                    SunriseOffset = new IntProperty((int)Properties.SunriseOffset.Value),
                    SunsetOffset = new IntProperty((int)Properties.SunsetOffset.Value),
                    Latitude = new StringProperty(Properties.Latitude.Value),
                    Longitude = new StringProperty(Properties.Longitude.Value),
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
