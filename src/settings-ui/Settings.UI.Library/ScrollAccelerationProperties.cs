// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ScrollAccelerationProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, true, true, false, 0x57); // Win + Ctrl + Alt + W

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [CmdConfigureIgnore]
        public HotkeySettings DefaultExcludeAppShortcut => new HotkeySettings(true, true, true, false, 0x58); // Win + Ctrl + Alt + X

        [JsonPropertyName("exclude_app_shortcut")]
        public HotkeySettings ExcludeAppShortcut { get; set; }

        // Executable names (one per line) that Scroll Acceleration leaves untouched.
        [JsonPropertyName("excluded_apps")]
        public StringProperty ExcludedApps { get; set; }

        public ScrollAccelerationProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            ExcludeAppShortcut = DefaultExcludeAppShortcut;
            ExcludedApps = new StringProperty();
            TimeThresholdMs = new IntProperty(100);
            AccelerationPercent = new IntProperty(100);
            SlowScrollPercent = new IntProperty(15);
            MaxMultiplier = new IntProperty(8);
            ApplyToHorizontalWheel = new BoolProperty(false);
            IgnoreWithCtrl = new BoolProperty(true);
            MomentumEnabled = new BoolProperty(false);
            MomentumDurationMs = new IntProperty(600);
        }

        [JsonPropertyName("time_threshold_ms")]
        public IntProperty TimeThresholdMs { get; set; }

        [JsonPropertyName("acceleration_percent")]
        public IntProperty AccelerationPercent { get; set; }

        [JsonPropertyName("slow_scroll_percent")]
        public IntProperty SlowScrollPercent { get; set; }

        [JsonPropertyName("max_multiplier")]
        public IntProperty MaxMultiplier { get; set; }

        [JsonPropertyName("apply_to_horizontal_wheel")]
        public BoolProperty ApplyToHorizontalWheel { get; set; }

        [JsonPropertyName("ignore_with_ctrl")]
        public BoolProperty IgnoreWithCtrl { get; set; }

        [JsonPropertyName("momentum_enabled")]
        public BoolProperty MomentumEnabled { get; set; }

        [JsonPropertyName("momentum_duration_ms")]
        public IntProperty MomentumDurationMs { get; set; }
    }
}
