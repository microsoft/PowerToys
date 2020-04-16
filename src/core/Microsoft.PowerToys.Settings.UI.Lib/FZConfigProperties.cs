using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class FZConfigProperties
    {
        public FZConfigProperties()
        {
            this.FancyzonesShiftDrag = new BoolProperty();
            this.FancyzonesOverrideSnapHotkeys = new BoolProperty();
            this.FancyzonesZoneSetChangeFlashZones = new BoolProperty();
            this.FancyzonesDisplayChangeMoveWindows = new BoolProperty();
            this.FancyzonesZoneSetChangeMoveWindows = new BoolProperty();
            this.FancyzonesVirtualDesktopChangeMoveWindows = new BoolProperty();
            this.FancyzonesAppLastZoneMoveWindows = new BoolProperty();
            this.UseCursorposEditorStartupscreen = new BoolProperty();
            this.FancyzonesShowOnAllMonitors = new BoolProperty();
            this.FancyzonesZoneHighlightColor = new StringProperty();
            this.FancyzonesHighlightOpacity = new IntProperty();
            this.FancyzonesEditorHotkey = new KeyBoardKeysProperty();
            this.FancyzonesExcludedApps = new StringProperty();
        }

        [JsonPropertyName("fancyzones_shiftDrag")]
        public BoolProperty FancyzonesShiftDrag { get; set; }

        [JsonPropertyName("fancyzones_overrideSnapHotkeys")]
        public BoolProperty FancyzonesOverrideSnapHotkeys { get; set; }

        [JsonPropertyName("fancyzones_zoneSetChange_flashZones")]
        public BoolProperty FancyzonesZoneSetChangeFlashZones { get; set; }

        [JsonPropertyName("fancyzones_displayChange_moveWindows")]
        public BoolProperty FancyzonesDisplayChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_zoneSetChange_moveWindows")]
        public BoolProperty FancyzonesZoneSetChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_virtualDesktopChange_moveWindows")]
        public BoolProperty FancyzonesVirtualDesktopChangeMoveWindows { get; set; }

        [JsonPropertyName("fancyzones_appLastZone_moveWindows")]
        public BoolProperty FancyzonesAppLastZoneMoveWindows { get; set; }

        [JsonPropertyName("use_cursorpos_editor_startupscreen")]
        public BoolProperty UseCursorposEditorStartupscreen { get; set; }

        [JsonPropertyName("fancyzones_show_on_all_monitors")]
        public BoolProperty FancyzonesShowOnAllMonitors { get; set; }

        [JsonPropertyName("fancyzones_zoneHighlightColor")]
        public StringProperty FancyzonesZoneHighlightColor { get; set; }

        [JsonPropertyName("fancyzones_highlight_opacity")]
        public IntProperty FancyzonesHighlightOpacity { get; set; }

        [JsonPropertyName("fancyzones_editor_hotkey")]
        public KeyBoardKeysProperty FancyzonesEditorHotkey { get; set; }

        [JsonPropertyName("fancyzones_excluded_apps")]
        public StringProperty FancyzonesExcludedApps { get; set; }
    }
}
