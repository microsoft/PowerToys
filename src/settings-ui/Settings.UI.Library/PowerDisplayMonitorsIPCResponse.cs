// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// IPC Response message for PowerDisplay monitors information
    /// </summary>
    public class PowerDisplayMonitorsIPCResponse
    {
        [JsonPropertyName("response_type")]
        public string ResponseType { get; set; } = "powerdisplay_monitors";

        [JsonPropertyName("monitors")]
        public List<MonitorInfoData> Monitors { get; set; } = new List<MonitorInfoData>();

        public PowerDisplayMonitorsIPCResponse(List<MonitorInfoData> monitors)
        {
            Monitors = monitors;
        }
    }

    /// <summary>
    /// Monitor information data for IPC
    /// </summary>
    public class MonitorInfoData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("internalName")]
        public string InternalName { get; set; } = string.Empty;

        [JsonPropertyName("hardwareId")]
        public string HardwareId { get; set; } = string.Empty;

        [JsonPropertyName("communicationMethod")]
        public string CommunicationMethod { get; set; } = string.Empty;

        [JsonPropertyName("monitorType")]
        public string MonitorType { get; set; } = string.Empty;

        [JsonPropertyName("currentBrightness")]
        public int CurrentBrightness { get; set; }

        [JsonPropertyName("colorTemperature")]
        public int ColorTemperature { get; set; }
    }
}
