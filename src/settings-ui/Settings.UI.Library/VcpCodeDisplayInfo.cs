// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Formatted VCP code display information
    /// </summary>
    public class VcpCodeDisplayInfo
    {
        private readonly List<VcpValueInfo> _valueList = new();

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("values")]
        public string Values { get; set; } = string.Empty;

        [JsonPropertyName("hasValues")]
        public bool HasValues { get; set; }

        [JsonPropertyName("valueList")]
        public List<VcpValueInfo> ValueList
        {
            get => _valueList;
            init => _valueList = value ?? new();
        }
    }
}
