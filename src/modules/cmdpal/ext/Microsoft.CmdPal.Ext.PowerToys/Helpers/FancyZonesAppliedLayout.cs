// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesAppliedLayout
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // blank|focus|columns|rows|grid|priority-grid|custom

    [JsonPropertyName("show-spacing")]
    public bool ShowSpacing { get; set; }

    [JsonPropertyName("spacing")]
    public int Spacing { get; set; }

    [JsonPropertyName("zone-count")]
    public int ZoneCount { get; set; }

    [JsonPropertyName("sensitivity-radius")]
    public int SensitivityRadius { get; set; }
}
