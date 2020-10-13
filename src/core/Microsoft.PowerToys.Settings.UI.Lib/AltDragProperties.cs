// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
   // public enum ColorRepresentationType
    // {
      //  HEX = 0,
        // RGB = 1,
    // }
    public class AltDragProperties
    {
        public AltDragProperties()
        {
            // ActivationShortcut = new HotkeySettings(true, false, false, true, 0x43);
            // ChangeCursor = false;
            HotkeyColor = new StringProperty("#F5FCFF");
        }

        // public HotkeySettings ActivationShortcut { get; set; }
        // [JsonPropertyName("changecursor")]
        // [JsonConverter(typeof(BoolPropertyJsonConverter))]
        // public bool ChangeCursor { get; set; }
        [JsonPropertyName("altdrag_hotkeyColor")]
        public StringProperty HotkeyColor { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
