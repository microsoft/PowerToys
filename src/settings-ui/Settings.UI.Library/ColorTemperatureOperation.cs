// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
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
    }
}
