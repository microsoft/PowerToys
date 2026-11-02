// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorBrakeDetectedMonitor
    {
        /// <summary>Gets or sets the monitor ID, empty when the module could not identify the monitor.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the friendly name, such as "Dell U2724D", or empty.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets the rect in physical pixels: left, top, right, bottom, with right and bottom outside.</summary>
        [JsonPropertyName("rect")]
        public List<int> Rect { get; init; } = new List<int>();

        [JsonPropertyName("dpi")]
        public int Dpi { get; set; }

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        /// <summary>Gets or sets Windows' display number for the monitor, or 0 when the file does not say.</summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>Gets or sets a value indicating whether the monitor is a panel built into the device.</summary>
        [JsonPropertyName("built_in")]
        public bool BuiltIn { get; set; }
    }
}
