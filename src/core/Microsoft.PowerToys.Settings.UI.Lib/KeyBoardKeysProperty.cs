// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeyBoardKeysProperty
    {
        public KeyBoardKeysProperty()
        {
            this.Value = new HotkeySettings();
        }

        public KeyBoardKeysProperty(HotkeySettings hkSettings)
        {
            this.Value = hkSettings;
        }

        [JsonPropertyName("value")]
        public HotkeySettings Value { get; set; }
    }
}
