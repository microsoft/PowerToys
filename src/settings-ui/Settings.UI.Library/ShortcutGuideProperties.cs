// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShortcutGuideProperties
    {
        public const int DefaultPressTimeMs = 900;
        public const int MinimumPressTimeMs = 100;
        public const int MaximumPressTimeMs = 5000;

        [CmdConfigureIgnore]
        public HotkeySettings DefaultOpenShortcutGuide => new HotkeySettings(true, false, false, true, 0xBF);

        public ShortcutGuideProperties()
        {
            WindowsKeyAction = new IntProperty((int)ShortcutGuideWindowsKeyAction.TaskbarIndicators);
            PressTime = new IntProperty(DefaultPressTimeMs);
            CloseOnWindowsKeyRelease = new BoolProperty(true);
            Theme = new StringProperty("system");
            DisabledApps = new StringProperty();
            OpenShortcutGuide = DefaultOpenShortcutGuide;
            FirstRun = new BoolProperty(true);
            WindowPosition = new IntProperty((int)ShortcutGuideWindowPosition.Left);
        }

        [JsonPropertyName("open_shortcutguide")]
        public HotkeySettings OpenShortcutGuide { get; set; }

        [JsonPropertyName("win_key_action")]
        public IntProperty WindowsKeyAction { get; set; }

        [JsonPropertyName("press_time")]
        public IntProperty PressTime { get; set; }

        [JsonPropertyName("close_on_windows_key_release")]
        public BoolProperty CloseOnWindowsKeyRelease { get; set; }

        [JsonPropertyName("theme")]
        public StringProperty Theme { get; set; }

        [JsonPropertyName("disabled_apps")]
        public StringProperty DisabledApps { get; set; }

        [JsonPropertyName("first_run")]
        public BoolProperty FirstRun { get; set; }

        // Migrated from StringProperty ("left" / "right") to IntProperty in v3.0.
        // The converter accepts both shapes so existing users' settings.json keeps working.
        [JsonPropertyName("window_position")]
        [JsonConverter(typeof(ShortcutGuideWindowPositionConverter))]
        public IntProperty WindowPosition { get; set; }
    }
}
