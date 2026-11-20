// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorBrakeSeenPair
    {
        /// <summary>Gets the two monitor IDs, in no particular order.</summary>
        [JsonPropertyName("monitors")]
        public List<string> Monitors { get; init; } = new List<string>();

        /// <summary>Gets or sets when the module last saw the two next to each other, as ISO 8601 UTC.</summary>
        [JsonPropertyName("last_seen_utc")]
        public string LastSeenUtc { get; set; } = string.Empty;
    }
}
