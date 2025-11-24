// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Represents a pending color temperature change operation
    /// </summary>
    public class ColorTemperatureOperation
    {
        [JsonPropertyName("monitor_id")]
        public string MonitorId { get; set; }

        [JsonPropertyName("color_temperature")]
        public int ColorTemperature { get; set; }

        public ColorTemperatureOperation()
        {
            MonitorId = string.Empty;
        }
    }
}
