// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public enum ColorRepresentationType
    {
        HEX = 0,
        RGB = 1,
    }

    public class ColorPickerProperties
    {
        public ColorPickerProperties()
        {
            ActivationShortcut = new HotkeySettings(false, true, false, false, "Break", 3);
            ChangeCursor = true;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("changecursor")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ChangeCursor { get; set; }

        [JsonPropertyName("copiedcolorrepresentation")]
        public ColorRepresentationType CopiedColorRepresentation { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
