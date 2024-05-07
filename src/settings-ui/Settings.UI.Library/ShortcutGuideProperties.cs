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
            OverlayOpacity = new IntProperty(90);
            UseLegacyPressWinKeyBehavior = new BoolProperty(false);
            PressTimeForGlobalWindowsShortcuts = new IntProperty(900);
            PressTimeForTaskbarIconShortcuts = new IntProperty(900);
            Theme = new StringProperty("system");
            DisabledApps = new StringProperty();
            OpenShortcutGuide = DefaultOpenShortcutGuide;
        }

        [JsonPropertyName("open_shortcutguide")]
        public HotkeySettings OpenShortcutGuide { get; set; }

        [JsonPropertyName("overlay_opacity")]
        public IntProperty OverlayOpacity { get; set; }

        [JsonPropertyName("use_legacy_press_win_key_behavior")]
        public BoolProperty UseLegacyPressWinKeyBehavior { get; set; }

        [JsonPropertyName("press_time")]
        public IntProperty PressTimeForGlobalWindowsShortcuts { get; set; }

        [JsonPropertyName("press_time_for_taskbar_icon_shortcuts")]
        public IntProperty PressTimeForTaskbarIconShortcuts { get; set; }

        [JsonPropertyName("theme")]
        public StringProperty Theme { get; set; }

        [JsonPropertyName("disabled_apps")]
        public StringProperty DisabledApps { get; set; }
    }
}
