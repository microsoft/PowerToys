// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Represents the configuration property of the settings that store Integer type.
    public record IntProperty : ICmdLineRepresentable
    {
        public IntProperty()
        {
            Value = 0;
        }

        public IntProperty(int value)
        {
            Value = value;
        }

        // Gets or sets the integer value of the settings configuration.
        [JsonPropertyName("value")]
        public int Value { get; set; }

        public static bool TryParseFromCmd(string cmd, out object result)
        {
            result = null;

            if (!int.TryParse(cmd, out var value))
            {
                return false;
            }

            result = new IntProperty { Value = value };
            return true;
        }

        // Returns a JSON version of the class settings configuration class.
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        public static implicit operator IntProperty(int v)
        {
            throw new NotImplementedException();
        }

        public static implicit operator IntProperty(uint v)
        {
            throw new NotImplementedException();
        }

        public bool TryToCmdRepresentable(out string result)
        {
            result = Value.ToString(CultureInfo.InvariantCulture);
            return true;
        }
    }
}
