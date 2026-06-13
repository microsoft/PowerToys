// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShortcutGuideProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultOpenShortcutGuide => new HotkeySettings(true, false, false, true, 0xBF);

        public ShortcutGuideProperties()
        {
            Theme = new StringProperty("system");
            DisabledApps = new StringProperty();
            OpenShortcutGuide = DefaultOpenShortcutGuide;
            FirstRun = new BoolProperty(true);
            WindowPosition = new IntProperty((int)ShortcutGuideWindowPosition.Left);
            UseLegacyPressWinKeyBehavior = new BoolProperty(false);
            PressTime = new IntProperty(DefaultPressTimeMs);
        }

        // Default press duration (ms) for the long-press Windows-key activation.
        // Matches the v0.99 default so users upgrading from the pre-V2 release pick up the same feel.
        public const int DefaultPressTimeMs = 900;

        [JsonPropertyName("open_shortcutguide")]
        public HotkeySettings OpenShortcutGuide { get; set; }

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

        // When true, Shortcut Guide opens on long-press of the Windows key (legacy v0.99 behavior)
        // and the customized shortcut is not registered. Mutually exclusive by design.
        [JsonPropertyName("use_legacy_press_win_key_behavior")]
        public BoolProperty UseLegacyPressWinKeyBehavior { get; set; }

        // Milliseconds the Windows key must be held before Shortcut Guide is shown.
        // Only honored when UseLegacyPressWinKeyBehavior is true.
        [JsonPropertyName("press_time")]
        public IntProperty PressTime { get; set; }
    }
}
