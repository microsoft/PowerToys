// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesAppliedDevice
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; set; } = string.Empty;

    [JsonPropertyName("monitor-instance")]
    public string MonitorInstance { get; set; } = string.Empty;

    [JsonPropertyName("monitor-number")]
    public int MonitorNumber { get; set; }

    [JsonPropertyName("serial-number")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("virtual-desktop")]
    public string VirtualDesktop { get; set; } = string.Empty;
}
