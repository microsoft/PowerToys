// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Values that must not be written to one VCP code. Values are persisted as
    /// numbers so display names and localization never affect the restriction.
    /// </summary>
    public class VcpValueBlock
    {
        private List<int> _values = new();

        [JsonPropertyName("vcpCode")]
        public byte VcpCode { get; set; }

        [JsonPropertyName("values")]
        public List<int> Values
        {
            get => _values;
            set => _values = value ?? new List<int>();
        }
    }
}
