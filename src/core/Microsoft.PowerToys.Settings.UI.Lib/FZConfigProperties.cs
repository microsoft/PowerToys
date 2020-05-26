using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class FZConfigProperties
    {
        public FZConfigProperties()
        {
            this.FancyzonesShiftDrag = new BoolProperty(ConfigDefaults.DefaultFancyzonesShiftDrag);
            this.FancyzonesOverrideSnapHotkeys = new BoolProperty();
            this.FancyzonesMouseSwitch = new BoolProperty();
            this.FancyzonesMoveWindowsAcrossMonitors = new BoolProperty();
            this.FancyzonesDisplayChangeMoveWindows = new BoolProperty();
            this.FancyzonesZoneSetChangeMoveWindows = new BoolProperty();
            this.FancyzonesAppLastZoneMoveWindows = new BoolProperty();
            this.UseCursorposEditorStartupscreen = new BoolProperty(ConfigDefaults.DefaultUseCursorposEditorStartupscreen);
            this.FancyzonesShowOnAllMonitors = new BoolProperty();
            this.FancyzonesZoneHighlightColor = new StringProperty(ConfigDefaults.DefaultFancyZonesZoneHighlightColor);
            this.FancyzonesHighlightOpacity = new IntProperty(50);
            this.FancyzonesEditorHotkey = new KeyBoardKeysProperty(
                new HotkeySettings()
                {
                    Win = true,
                    Ctrl = false,
                    Alt = false,
                    Shift = false,
                    Key = "`",
                    Code = 192,
                });
            this.FancyzonesMakeDraggedWindowTransparent = new BoolProperty();
            this.FancyzonesExcludedApps = new StringProperty();
            this.FancyzonesInActiveColor = new StringProperty(ConfigDefaults.DefaultFancyZonesInActiveColor);
            this.FancyzonesBorderColor = new StringProperty(ConfigDefaults.DefaultFancyzonesBorderColor);
        }

        [JsonPropertyName("fancyzones_shiftDrag")]
        public BoolProperty FancyzonesShiftDrag { get; set; }

        [JsonPropertyName("fancyzones_mouseSwitch")]
        public BoolProperty FancyzonesMouseSwitch { get; set; }

        [JsonPropertyName("fancyzones_overrideSnapHotkeys")]
        public BoolProperty FancyzonesOverrideSnapHotkeys { get; set; }

        [JsonPropertyName("fancyzones_moveWindowAcrossMonitors")]
        public BoolProperty FancyzonesMoveWindowsAcrossMonitors { get; set; }

        [JsonPropertyName("fancyzones_displayChange_moveWindows")]
        public BoolProperty FancyzonesDisplayChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_zoneSetChange_moveWindows")]
        public BoolProperty FancyzonesZoneSetChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_appLastZone_moveWindows")]
        public BoolProperty FancyzonesAppLastZoneMoveWindows { get; set; }

        [JsonPropertyName("use_cursorpos_editor_startupscreen")]
        public BoolProperty UseCursorposEditorStartupscreen { get; set; }

        [JsonPropertyName("fancyzones_show_on_all_monitors")]
        public BoolProperty FancyzonesShowOnAllMonitors { get; set; }

        [JsonPropertyName("fancyzones_makeDraggedWindowTransparent")]
        public BoolProperty FancyzonesMakeDraggedWindowTransparent { get; set; }

        [JsonPropertyName("fancyzones_zoneHighlightColor")]
        public StringProperty FancyzonesZoneHighlightColor { get; set; }

        [JsonPropertyName("fancyzones_highlight_opacity")]
        public IntProperty FancyzonesHighlightOpacity { get; set; }

        [JsonPropertyName("fancyzones_editor_hotkey")]
        public KeyBoardKeysProperty FancyzonesEditorHotkey { get; set; }

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
