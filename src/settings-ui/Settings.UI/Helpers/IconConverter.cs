// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public partial class IconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string iconValue || string.IsNullOrEmpty(iconValue))
            {
                // Return a default icon based on the parameter
                var defaultGlyph = parameter?.ToString() ?? "\uE8B7"; // Default folder icon
                return new FontIcon { Glyph = defaultGlyph };
            }

            // Check if it's a single Unicode character (most common case after JSON deserialization)
            if (iconValue.Length == 1)
            {
                return new FontIcon { Glyph = iconValue };
            }

            // Check if it's still in \uXXXX string format (shouldn't happen after JSON deserialization)
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
            if (iconValue.Contains('/') || iconValue.Contains('\\') || iconValue.Contains(".png") || iconValue.Contains(".jpg") || iconValue.Contains(".ico"))
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

                return new BitmapIcon
                {
                    UriSource = new Uri($"ms-appx://{imagePath}"),
                    ShowAsMonochrome = false,
                };
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
