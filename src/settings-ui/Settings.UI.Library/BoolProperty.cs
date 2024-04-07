// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public record BoolProperty : ICmdLineRepresentable
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

        public static bool TryParseFromCmd(string cmd, out object result)
        {
            result = null;

            if (!bool.TryParse(cmd, out bool value))
            {
                return false;
            }

            result = new BoolProperty { Value = value };
            return true;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        public bool TryToCmdRepresentable(out string result)
        {
            result = Value.ToString();
            return true;
        }
    }
}
