// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class MonitorDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("instance-id")]
        public string InstanceId { get; set; } = string.Empty;

        [JsonPropertyName("monitor-number")]
        public int Number { get; set; }

        [JsonPropertyName("dpi")]
        public int Dpi { get; set; }

        [JsonPropertyName("monitor-rect-dpi-aware")]
        public MonitorRect DpiAwareRect { get; set; } = new();

        [JsonPropertyName("monitor-rect-dpi-unaware")]
        public MonitorRect DpiUnawareRect { get; set; } = new();

        internal sealed class MonitorRect
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
    }
}
