// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesEditorMonitor
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; set; } = string.Empty;

    [JsonPropertyName("monitor-instance-id")]
    public string MonitorInstanceId { get; set; } = string.Empty;

    [JsonPropertyName("monitor-serial-number")]
    public string MonitorSerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("monitor-number")]
    public int MonitorNumber { get; set; }

    [JsonPropertyName("virtual-desktop")]
    public string VirtualDesktop { get; set; } = string.Empty;

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; }

    [JsonPropertyName("left-coordinate")]
    public int LeftCoordinate { get; set; }

    [JsonPropertyName("top-coordinate")]
    public int TopCoordinate { get; set; }

    [JsonPropertyName("work-area-width")]
    public int WorkAreaWidth { get; set; }

    [JsonPropertyName("work-area-height")]
    public int WorkAreaHeight { get; set; }

    [JsonPropertyName("monitor-width")]
    public int MonitorWidth { get; set; }

    [JsonPropertyName("monitor-height")]
    public int MonitorHeight { get; set; }

    [JsonPropertyName("is-selected")]
    public bool IsSelected { get; set; }
}
