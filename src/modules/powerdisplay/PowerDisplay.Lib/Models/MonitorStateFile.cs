// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Monitor state file structure for JSON persistence.
    /// Contains all monitor states indexed by Monitor.Id.
    /// </summary>
    public sealed class MonitorStateFile
    {
        /// <summary>
        /// Gets or sets the monitor states dictionary.
        /// Key is the monitor's unique Id (e.g., "DDC_GSM5C6D_1", "WMI_BOE0900_2").
        /// </summary>
        [JsonPropertyName("monitors")]
        public Dictionary<string, MonitorStateEntry> Monitors { get; set; } = new();

        /// <summary>
        /// Gets or sets when the file was last updated.
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}
