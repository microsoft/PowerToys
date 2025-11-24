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

        /// <summary>
        /// Gets or sets the color temperature VCP preset value.
        /// JSON property name kept as "color_temperature" for IPC compatibility.
        /// </summary>
        [JsonPropertyName("color_temperature")]
        public int ColorTemperatureVcp { get; set; }

        public ColorTemperatureOperation()
        {
            MonitorId = string.Empty;
        }
    }
}
