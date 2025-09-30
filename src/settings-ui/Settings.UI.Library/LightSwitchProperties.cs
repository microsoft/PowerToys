// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class LightSwitchProperties
    {
        public const bool DefaultChangeSystem = true;
        public const bool DefaultChangeApps = true;
        public const int DefaultLightTime = 480;
        public const int DefaultDarkTime = 1200;
        public const int DefaultSunriseOffset = 0;
        public const int DefaultSunsetOffset = 0;
        public const string DefaultLatitude = "0.0";
        public const string DefaultLongitude = "0.0";
        public const string DefaultScheduleMode = "FixedHours";
        public static readonly HotkeySettings DefaultToggleThemeHotkey = new HotkeySettings(true, true, false, true, 0x44); // Ctrl+Win+Shift+D

        public LightSwitchProperties()
        {
            ChangeSystem = new BoolProperty(DefaultChangeSystem);
            ChangeApps = new BoolProperty(DefaultChangeApps);
            LightTime = new IntProperty(DefaultLightTime);
            DarkTime = new IntProperty(DefaultDarkTime);
            Latitude = new StringProperty(DefaultLatitude);
            Longitude = new StringProperty(DefaultLongitude);
            SunriseOffset = new IntProperty(DefaultSunriseOffset);
            SunsetOffset = new IntProperty(DefaultSunsetOffset);
            ScheduleMode = new StringProperty(DefaultScheduleMode);
            ToggleThemeHotkey = new KeyboardKeysProperty(DefaultToggleThemeHotkey);
        }

        [JsonPropertyName("changeSystem")]
        public BoolProperty ChangeSystem { get; set; }

        [JsonPropertyName("changeApps")]
        public BoolProperty ChangeApps { get; set; }

        [JsonPropertyName("lightTime")]
        public IntProperty LightTime { get; set; }

        [JsonPropertyName("darkTime")]
        public IntProperty DarkTime { get; set; }

        [JsonPropertyName("sunrise_offset")]
        public IntProperty SunriseOffset { get; set; }

        [JsonPropertyName("sunset_offset")]
        public IntProperty SunsetOffset { get; set; }

        [JsonPropertyName("latitude")]
        public StringProperty Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public StringProperty Longitude { get; set; }

        [JsonPropertyName("scheduleMode")]
        public StringProperty ScheduleMode { get; set; }

        [JsonPropertyName("toggle-theme-hotkey")]
        public KeyboardKeysProperty ToggleThemeHotkey { get; set; }
    }
}
