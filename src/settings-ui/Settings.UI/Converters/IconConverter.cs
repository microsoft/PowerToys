// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public partial class IconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string iconValue || string.IsNullOrEmpty(iconValue))
            {
                // Return a default icon based on the parameter
                var defaultGlyph = parameter?.ToString() ?? "\uE8B7"; // Default gear icon
                return new FontIcon { Glyph = defaultGlyph };
            }

            // Check if it's a single Unicode character (most common case after JSON deserialization)
            if (iconValue.Length == 1)
            {
                return new FontIcon { Glyph = iconValue };
            }

            // Handle HTML numeric character references, e.g. "&#xE80F;" or "&#59951;"
            if (iconValue.StartsWith("&#", StringComparison.Ordinal) && iconValue.EndsWith(';'))
            {
                var inner = iconValue.Substring(2, iconValue.Length - 3); // strip &# and ;
                try
                {
                    string glyph;
                    if (inner.StartsWith("x", StringComparison.OrdinalIgnoreCase))
                    {
                        var hex = inner.Substring(1);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePointHex))
                        {
                            glyph = char.ConvertFromUtf32(codePointHex);
                            return new FontIcon { Glyph = glyph };
                        }
                    }
                    else if (int.TryParse(inner, out int codePointDec))
                    {
                        glyph = char.ConvertFromUtf32(codePointDec);
                        return new FontIcon { Glyph = glyph };
                    }
                }
                catch
                {
                    // fall through to other handlers
                }
            }

            if (iconValue.StartsWith("\\u", StringComparison.OrdinalIgnoreCase) && iconValue.Length == 6)
            {
                var hexPart = iconValue.Substring(2); // Remove \u
                if (int.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                {
                    var unicodeChar = char.ConvertFromUtf32(codePoint);
                    return new FontIcon { Glyph = unicodeChar };
                }
            }

            // Check if it's an image path
            if (iconValue.Contains('/') || iconValue.Contains('\\') || iconValue.Contains(".png", StringComparison.OrdinalIgnoreCase) || iconValue.Contains(".jpg", StringComparison.OrdinalIgnoreCase) || iconValue.Contains(".ico", StringComparison.OrdinalIgnoreCase) || iconValue.Contains(".svg", StringComparison.OrdinalIgnoreCase))
            {
                // Handle different path formats
                var imagePath = iconValue;

                // Convert ms-appx:/// paths to local paths
                if (imagePath.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase))
                {
                    imagePath = imagePath.Substring("ms-appx:///".Length);
                }

                // Ensure path starts with /
                if (!imagePath.StartsWith('/'))
                {
                    imagePath = "/" + imagePath;
                }

                var uri = new Uri($"ms-appx://{imagePath}");

                if (imagePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    // Render SVG using ImageIcon + SvgImageSource
                    return new ImageIcon
                    {
                        Source = new SvgImageSource(uri),
                    };
                }
                else
                {
                    return new BitmapIcon
                    {
                        UriSource = uri,
                        ShowAsMonochrome = false,
                    };
                }
            }

            // Try to interpret as raw SVG path data (PathIcon.Data)
            // Many of our XAML PathIcon usages (e.g., AdvancedPastePage) provide a Data string like "M128 766q0-42 ...".
            // If parsing succeeds, render it as a PathIcon.
            try
            {
                var geometryObj = XamlBindingHelper.ConvertValue(typeof(Geometry), iconValue);
                if (geometryObj is Geometry geometry)
                {
                    return new PathIcon { Data = geometry };
                }
            }
            catch
            {
                // Ignore parse errors and fall back below.
            }

            // If all else fails, return default icon
            var fallbackGlyph = parameter?.ToString() ?? "\uE8B7";
            return new FontIcon { Glyph = fallbackGlyph };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
