// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Represents the configuration property of the settings that store string type.
    public record StringProperty : ICmdLineRepresentable
    {
        public StringProperty()
        {
            Value = string.Empty;
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

        public static StringProperty ToStringProperty(string v)
        {
            return new StringProperty(v);
        }

        public static bool TryParseFromCmd(string cmd, out object result)
        {
            result = new StringProperty(cmd);
            return true;
        }

        public bool TryToCmdRepresentable(out string result)
        {
            result = Value;
            return true;
        }

        public static implicit operator StringProperty(string v)
        {
            return new StringProperty(v);
        }
    }
}
