// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// One entry of CursorBrake's <c>edges</c> setting: how crossings between two monitors behave.
    /// </summary>
    public class CursorBrakeEdgeSetting
    {
        // The entry's field names, which CursorBrakeEdgesConverter reads and writes too.
        public const string MonitorsName = "monitors";
        public const string OneWayName = "one_way";
        public const string ModeName = "mode";
        public const string PushDistanceName = "push_distance";

        /// <summary>
        /// Gets the two monitor IDs, in PowerDisplay's <c>\\?\DISPLAY#&lt;EDID&gt;#&lt;instance&gt;</c> form.
        /// </summary>
        [JsonPropertyName(MonitorsName)]
        public List<string> Monitors { get; init; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether the entry covers only crossings from the first
        /// monitor to the second. Left out of the file when false, which covers both directions.
        /// </summary>
        [JsonPropertyName(OneWayName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool OneWay { get; set; }

        /// <summary>
        /// Gets or sets the mode: Custom, Off or Wall. Any other value is kept as read, and the module
        /// skips the entry.
        /// </summary>
        [JsonPropertyName(ModeName)]
        public CursorBrakeEdgeMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the push distance in logical pixels, 0 to 1000. Off and Wall keep it, so
        /// switching back to Custom restores it.
        /// </summary>
        [JsonPropertyName(PushDistanceName)]
        public int PushDistance { get; set; } = CursorBrakeProperties.DefaultPushDistance;
    }
}
