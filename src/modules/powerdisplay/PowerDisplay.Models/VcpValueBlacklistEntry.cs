// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Known problematic values for one VCP code on a monitor model. Unlike a whole
    /// monitor blacklist entry, this restriction only prevents matching writes.
    /// </summary>
    public class VcpValueBlacklistEntry : VcpValueBlock
    {
        [JsonPropertyName("edidId")]
        public string EdidId { get; set; } = string.Empty;

        [JsonPropertyName("comments")]
        public string Comments { get; set; } = string.Empty;
    }
}
