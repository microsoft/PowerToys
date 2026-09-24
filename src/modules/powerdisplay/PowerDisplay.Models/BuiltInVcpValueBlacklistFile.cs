// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Versioned JSON shape for the built-in restrictions on VCP values.
    /// </summary>
    public class BuiltInVcpValueBlacklistFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("entries")]
        public List<VcpValueBlacklistEntry> Entries { get; set; } = new();
    }
}
