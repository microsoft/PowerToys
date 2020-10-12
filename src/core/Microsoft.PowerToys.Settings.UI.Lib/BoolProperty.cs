// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class BoolProperty
    {
        public BoolProperty()
        {
            Value = false;
        }

        public BoolProperty(bool value)
        {
            Value = value;
        }

        [JsonPropertyName("value")]
        public bool Value { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
