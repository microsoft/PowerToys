// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Message for PowerDisplay module actions
    /// </summary>
    public class PowerDisplayActionMessage
    {
        [JsonPropertyName("action")]
        public ActionData Action { get; set; }

        public class ActionData
        {
            [JsonPropertyName("PowerDisplay")]
            public PowerDisplayAction PowerDisplay { get; set; }
        }

        public class PowerDisplayAction
        {
            [JsonPropertyName("action_name")]
            public string ActionName { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }

            [JsonPropertyName("monitor_id")]
            public string MonitorId { get; set; }

            [JsonPropertyName("color_temperature")]
            public int ColorTemperature { get; set; }
        }
    }
}
