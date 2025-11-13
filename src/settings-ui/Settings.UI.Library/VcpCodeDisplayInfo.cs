// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Formatted VCP code display information
    /// </summary>
    public class VcpCodeDisplayInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("values")]
        public string Values { get; set; } = string.Empty;

        [JsonPropertyName("hasValues")]
        public bool HasValues { get; set; }
    }
}
