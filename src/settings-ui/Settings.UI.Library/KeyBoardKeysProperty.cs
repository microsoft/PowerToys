// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public record KeyboardKeysProperty : ICmdLineRepresentable
    {
        public KeyboardKeysProperty()
        {
            Value = new HotkeySettings();
        }

        public KeyboardKeysProperty(HotkeySettings hkSettings)
        {
            Value = hkSettings;
        }

        [JsonPropertyName("value")]
        public HotkeySettings Value { get; set; }

        public static bool TryParseFromCmd(string cmd, out object result)
        {
            if (!HotkeySettings.TryParseFromCmd(cmd, out var hotkey))
            {
                result = null;
                return false;
            }
            else
            {
                result = new KeyboardKeysProperty { Value = (HotkeySettings)hotkey };
                return true;
            }
        }

        public bool TryToCmdRepresentable(out string result)
        {
            return Value.TryToCmdRepresentable(out result);
        }
    }
}
