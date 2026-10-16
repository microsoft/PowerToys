// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Calibration data lives in calibration.json, which only the native module writes.
    public class SeamlessCursorProperties
    {
        // Where no monitor is physically across: stop the pointer, or move it to the nearest monitor.
        [JsonPropertyName("stop_at_empty_edges")]
        public BoolProperty StopAtEmptyEdges { get; set; }

        // When CursorWrap wraps the pointer, land at the same physical position on calibrated monitors.
        [JsonPropertyName("map_cursor_wrap")]
        public BoolProperty MapCursorWrap { get; set; }

        public SeamlessCursorProperties()
        {
            StopAtEmptyEdges = new BoolProperty(true);
            MapCursorWrap = new BoolProperty(true);
        }
    }
}
