// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FZConfigProperties
    {
        public const int VkOem3 = 0xc0;
        public const int VkNext = 0x22;
        public const int VkPrior = 0x21;

        public static readonly HotkeySettings DefaultEditorHotkeyValue = new HotkeySettings(true, false, false, false, VkOem3);
        public static readonly HotkeySettings DefaultNextTabHotkeyValue = new HotkeySettings(true, false, false, false, VkNext);
        public static readonly HotkeySettings DefaultPrevTabHotkeyValue = new HotkeySettings(true, false, false, false, VkPrior);

        public FZConfigProperties()
        {
            FancyzonesShiftDrag = new BoolProperty(ConfigDefaults.DefaultFancyzonesShiftDrag);
            FancyzonesOverrideSnapHotkeys = new BoolProperty();
            FancyzonesMouseSwitch = new BoolProperty();
            FancyzonesMoveWindowsAcrossMonitors = new BoolProperty();
            FancyzonesMoveWindowsBasedOnPosition = new BoolProperty();
            FancyzonesDisplayChangeMoveWindows = new BoolProperty();
            FancyzonesZoneSetChangeMoveWindows = new BoolProperty();
            FancyzonesAppLastZoneMoveWindows = new BoolProperty();
            FancyzonesOpenWindowOnActiveMonitor = new BoolProperty();
            FancyzonesRestoreSize = new BoolProperty();
            UseCursorposEditorStartupscreen = new BoolProperty(ConfigDefaults.DefaultUseCursorposEditorStartupscreen);
            FancyzonesShowOnAllMonitors = new BoolProperty();
            FancyzonesSpanZonesAcrossMonitors = new BoolProperty();
            FancyzonesZoneHighlightColor = new StringProperty(ConfigDefaults.DefaultFancyZonesZoneHighlightColor);
            FancyzonesHighlightOpacity = new IntProperty(50);
            FancyzonesEditorHotkey = new KeyboardKeysProperty(DefaultEditorHotkeyValue);
            FancyzonesNextTabHotkey = new KeyboardKeysProperty(DefaultNextTabHotkeyValue);
            FancyzonesPrevTabHotkey = new KeyboardKeysProperty(DefaultPrevTabHotkeyValue);
            FancyzonesMakeDraggedWindowTransparent = new BoolProperty();
            FancyzonesExcludedApps = new StringProperty();
            FancyzonesInActiveColor = new StringProperty(ConfigDefaults.DefaultFancyZonesInActiveColor);
            FancyzonesBorderColor = new StringProperty(ConfigDefaults.DefaultFancyzonesBorderColor);
        }

        [JsonPropertyName("fancyzones_shiftDrag")]
        public BoolProperty FancyzonesShiftDrag { get; set; }

        [JsonPropertyName("fancyzones_mouseSwitch")]
        public BoolProperty FancyzonesMouseSwitch { get; set; }

        [JsonPropertyName("fancyzones_overrideSnapHotkeys")]
        public BoolProperty FancyzonesOverrideSnapHotkeys { get; set; }

        [JsonPropertyName("fancyzones_moveWindowAcrossMonitors")]
        public BoolProperty FancyzonesMoveWindowsAcrossMonitors { get; set; }

        [JsonPropertyName("fancyzones_moveWindowsBasedOnPosition")]
        public BoolProperty FancyzonesMoveWindowsBasedOnPosition { get; set; }

        [JsonPropertyName("fancyzones_displayChange_moveWindows")]
        public BoolProperty FancyzonesDisplayChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_zoneSetChange_moveWindows")]
        public BoolProperty FancyzonesZoneSetChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_appLastZone_moveWindows")]
        public BoolProperty FancyzonesAppLastZoneMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_openWindowOnActiveMonitor")]
        public BoolProperty FancyzonesOpenWindowOnActiveMonitor { get; set; }

        [JsonPropertyName("fancyzones_restoreSize")]
        public BoolProperty FancyzonesRestoreSize { get; set; }

        [JsonPropertyName("use_cursorpos_editor_startupscreen")]
        public BoolProperty UseCursorposEditorStartupscreen { get; set; }

        [JsonPropertyName("fancyzones_show_on_all_monitors")]
        public BoolProperty FancyzonesShowOnAllMonitors { get; set; }

        [JsonPropertyName("fancyzones_span_zones_across_monitors")]
        public BoolProperty FancyzonesSpanZonesAcrossMonitors { get; set; }

        [JsonPropertyName("fancyzones_makeDraggedWindowTransparent")]
        public BoolProperty FancyzonesMakeDraggedWindowTransparent { get; set; }

        [JsonPropertyName("fancyzones_zoneHighlightColor")]
        public StringProperty FancyzonesZoneHighlightColor { get; set; }

        [JsonPropertyName("fancyzones_highlight_opacity")]
        public IntProperty FancyzonesHighlightOpacity { get; set; }

        [JsonPropertyName("fancyzones_editor_hotkey")]
        public KeyboardKeysProperty FancyzonesEditorHotkey { get; set; }

        [JsonPropertyName("fancyzones_nextTab_hotkey")]
        public KeyboardKeysProperty FancyzonesNextTabHotkey { get; set; }

        [JsonPropertyName("fancyzones_prevTab_hotkey")]
        public KeyboardKeysProperty FancyzonesPrevTabHotkey { get; set; }

        [JsonPropertyName("fancyzones_excluded_apps")]
        public StringProperty FancyzonesExcludedApps { get; set; }

        [JsonPropertyName("fancyzones_zoneBorderColor")]
        public StringProperty FancyzonesBorderColor { get; set; }

        [JsonPropertyName("fancyzones_zoneColor")]
        public StringProperty FancyzonesInActiveColor { get; set; }

        // converts the current to a json string.
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
