// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public enum ColorRepresentationType
    {
        /// <summary>
        /// Color presentation as hexadecimal color value without the alpha-value (e.g. #0055FF)
        /// </summary>
        HEX = 0,

        /// <summary>
        /// Color presentation as RGB color value (red[0..255], green[0..255], blue[0..255])
        /// </summary>
        RGB = 1,

        /// <summary>
        /// Color presentation as CMYK color value (cyan[0%..100%], magenta[0%..100%], yellow[0%..100%], black key[0%..100%])
        /// </summary>
        CMYK = 2,

        /// <summary>
        /// Color presentation as HSL color value (hue[0°..360°], saturation[0..100%], lightness[0%..100%])
        /// </summary>
        HSL = 3,

        /// <summary>
        /// Color presentation as HSV color value (hue[0°..360°], saturation[0%..100%], value[0%..100%])
        /// </summary>
        HSV = 4,
    }

    public class ColorPickerProperties
    {
        public ColorPickerProperties()
        {
            ActivationShortcut = new HotkeySettings(true, false, false, true, 0x43);
            ChangeCursor = false;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("changecursor")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ChangeCursor { get; set; }

        [JsonPropertyName("copiedcolorrepresentation")]
        public ColorRepresentationType CopiedColorRepresentation { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
