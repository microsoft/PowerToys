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
}
