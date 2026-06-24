// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Converters
{
    public static class HexColorConverterHelper
    {
        public static Windows.UI.Color? ConvertHexColorToRgb(string hexColor)
        {
            try
            {
                // Remove # if present
                var cleanHex = hexColor.TrimStart('#');

                // Expand 3-digit hex to 6-digit (#ABC -> #AABBCC)
                if (cleanHex.Length == 3)
                {
                    cleanHex = $"{cleanHex[0]}{cleanHex[0]}{cleanHex[1]}{cleanHex[1]}{cleanHex[2]}{cleanHex[2]}";
                }

                if (cleanHex.Length == 6)
                {
                    var r = System.Convert.ToByte(cleanHex.Substring(0, 2), 16);
                    var g = System.Convert.ToByte(cleanHex.Substring(2, 2), 16);
                    var b = System.Convert.ToByte(cleanHex.Substring(4, 2), 16);

                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
                // Invalid color format - return null
            }

            return null;
        }
    }
}
