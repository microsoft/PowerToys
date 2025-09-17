// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace Awake.Core.Usage.Models
{
    internal sealed class AppUsageRecord
    {
        [JsonPropertyName("process")]
        public string ProcessName { get; set; } = string.Empty;

        [JsonPropertyName("totalSeconds")]
        public double TotalSeconds { get; set; }

        [JsonPropertyName("lastUpdatedUtc")]
        public DateTime LastUpdatedUtc { get; set; }

        [JsonPropertyName("firstSeenUtc")]
        public DateTime FirstSeenUtc { get; set; }
    }
}
