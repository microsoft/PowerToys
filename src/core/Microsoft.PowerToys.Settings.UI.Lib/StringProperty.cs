// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    // Represents the configuration property of the settings that store string type.
    public class StringProperty
    {
        public StringProperty()
        {
            this.Value = string.Empty;
        }

        public StringProperty(string value)
        {
            Value = value;
        }

        // Gets or sets the integer value of the settings configuration.
        [JsonPropertyName("value")]
        public string Value { get; set; }

        // Returns a JSON version of the class settings configuration class.
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
