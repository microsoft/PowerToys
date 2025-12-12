// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI.Helpers
{
    public static class CustomColorHelper
    {
        public static SolidColorBrush GetBrushFromHex(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    return new SolidColorBrush(Colors.Transparent);
                }

                // Handles #RRGGBB or #AARRGGBB
                hex = hex.Replace("#", string.Empty);
                byte a = 255;
                byte r = 0, g = 0, b = 0;

                var provider = CultureInfo.InvariantCulture;

                if (hex.Length == 6)
                {
                    r = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, provider);
                    g = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, provider);
                    b = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, provider);
                }
                else if (hex.Length == 8)
                {
                    a = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, provider);
                    r = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, provider);
                    g = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, provider);
                    b = byte.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber, provider);
                }

                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch
            {
                // Error fallback
                return new SolidColorBrush(Colors.Magenta);
            }
        }
    }
}
