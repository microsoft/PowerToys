// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Needs to stay in sync with src\modules\darkmode\DarkMode\DarkModeSettings.h
    public class DarkModeProperties
    {
        public const bool DefaultChangeSystem = false;
        public const bool DefaultChangeApps = false;
        public const int DefaultLightTime = 480;   // 08:00
        public const int DefaultDarkTime = 1200;   // 20:00
        public const string DefaultLatitude = "0.0";
        public const string DefaultLongitude = "0.0";
        public const string DefaultScheduleMode = "FixedHours";

        public DarkModeProperties()
        {
            ChangeSystem = new BoolProperty(DefaultChangeSystem);
            ChangeApps = new BoolProperty(DefaultChangeApps);
            LightTime = new IntProperty(DefaultLightTime);
            DarkTime = new IntProperty(DefaultDarkTime);
            Latitude = new StringProperty(DefaultLatitude);
            Longitude = new StringProperty(DefaultLongitude);
            ScheduleMode = new StringProperty(DefaultScheduleMode);
        }

        [JsonPropertyName("changeSystem")]
        public BoolProperty ChangeSystem { get; set; }

        [JsonPropertyName("changeApps")]
        public BoolProperty ChangeApps { get; set; }

        [JsonPropertyName("lightTime")]
        public IntProperty LightTime { get; set; }

        [JsonPropertyName("darkTime")]
        public IntProperty DarkTime { get; set; }

        [JsonPropertyName("latitude")]
        public StringProperty Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public StringProperty Longitude { get; set; }

        [JsonPropertyName("scheduleMode")]
        public StringProperty ScheduleMode { get; set; }
    }
}
