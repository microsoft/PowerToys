// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// JSON file shape for <see cref="BuiltInMonitorBlacklist"/>.
    /// The <see cref="Version"/> field is a forward-compatibility marker; this
    /// release only understands version 1.
    /// </summary>
    public class BuiltInMonitorBlacklistFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("entries")]
        public List<MonitorBlacklistEntry> Entries { get; set; } = new();
    }
}
