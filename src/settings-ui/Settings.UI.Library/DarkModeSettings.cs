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
    public class DarkModeSettings : BasePTModuleSettings, ISettingsConfig, ICloneable
    {
        public const string ModuleName = "DarkMode";

        public DarkModeSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new DarkModeProperties();
        }

        [JsonPropertyName("properties")]
        public DarkModeProperties Properties { get; set; }

        public object Clone()
        {
            return new DarkModeSettings()
            {
                Name = Name,
                Version = Version,
                Properties = new DarkModeProperties()
                {
                    ChangeSystem = Properties.ChangeSystem,
                    ChangeApps = Properties.ChangeApps,
                    UseLocation = Properties.UseLocation,
                    LightTime = Properties.LightTime,
                    DarkTime = Properties.DarkTime,
                    Latitude = Properties.Latitude,
                    Longitude = Properties.Longitude,
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
