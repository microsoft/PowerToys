// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace WorkspacesCsharpLibrary.Data;

public struct MonitorConfigurationWrapper
{
    public struct MonitorRectWrapper
    {
        [JsonPropertyName("top")]
        public int Top { get; set; }

        [JsonPropertyName("left")]
        public int Left { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("instance-id")]
    public string InstanceId { get; set; }

    [JsonPropertyName("monitor-number")]
    public int MonitorNumber { get; set; }

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; }

    [JsonPropertyName("monitor-rect-dpi-aware")]
    public MonitorRectWrapper MonitorRectDpiAware { get; set; }

    [JsonPropertyName("monitor-rect-dpi-unaware")]
    public MonitorRectWrapper MonitorRectDpiUnaware { get; set; }
}
