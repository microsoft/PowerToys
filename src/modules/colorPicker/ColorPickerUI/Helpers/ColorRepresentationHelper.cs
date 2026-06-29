// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;

using ManagedCommon;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// Helper class to easier work with color representation
    /// </summary>
    public static class ColorRepresentationHelper
    {
        /// <summary>
        /// Return a <see cref="string"/> representation of a given <see cref="Color"/>
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the presentation</param>
        /// <param name="colorRepresentationType">The type of the representation</param>
        /// <returns>A <see cref="string"/> representation of a color</returns>
        internal static string GetStringRepresentationFromMediaColor(Windows.UI.Color color, string colorRepresentationType)
        {
            var drawingcolor = Color.FromArgb(color.A, color.R, color.G, color.B);
            return GetStringRepresentation(drawingcolor, colorRepresentationType, string.Empty);
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a given <see cref="Color"/>
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the presentation</param>
        /// <param name="colorRepresentationType">The type of the representation</param>
        /// <returns>A <see cref="string"/> representation of a color</returns>
        public static string GetStringRepresentation(Color color, string colorRepresentationType, string colorFormat)
        {
            if (string.IsNullOrEmpty(colorFormat))
            {
                return ColorToHex(color);
            }
            else
            {
                // get string representation in 2 steps. First replace all color specific number values then in 2nd step replace color name with localisation
                return ReplaceName(ColorFormatHelper.GetStringRepresentation(color, colorFormat), color);
            }
        }

        /// <summary>
        /// Return a hexadecimal <see cref="string"/> representation of a RGB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the hexadecimal presentation</param>
        /// <returns>A hexadecimal <see cref="string"/> representation of a RGB color</returns>
        private static string ColorToHex(Color color)
        {
            const string hexFormat = "x2";

            return $"{color.R.ToString(hexFormat, CultureInfo.InvariantCulture)}"
                + $"{color.G.ToString(hexFormat, CultureInfo.InvariantCulture)}"
                + $"{color.B.ToString(hexFormat, CultureInfo.InvariantCulture)}";
        }

        public static string GetColorNameFromColorIdentifier(string colorIdentifier)
        {
            // The color-name identifiers (e.g. "TEXT_COLOR_BLACK") are the .resw resource keys
            // verbatim, so resolve them directly through the resource loader. This replaces the
            // WPF-era switch over the generated Resources designer, which no longer exists after
            // the WinUI 3 migration moved the string table from Properties\Resources.resx to
            // Strings\en-us\Resources.resw.
            return ResourceLoaderInstance.GetString(colorIdentifier);
        }

        public static string ReplaceName(string colorFormat, Color color)
        {
            var colorNameParameter = ColorFormatHelper.GetColorNameParameter();

            // Only resolve the (localized, resource-backed) color name when the format string
            // actually contains the name placeholder. This skips a resource lookup for the common
            // numeric formats and keeps pure-conversion unit tests from requiring the MRT resource
            // loader (and the module .pri) to be initialized in the test host.
            if (!colorFormat.Contains(colorNameParameter, StringComparison.Ordinal))
            {
                return colorFormat;
            }

            return colorFormat.Replace(colorNameParameter, GetColorNameFromColorIdentifier(ColorNameHelper.GetColorNameIdentifier(color)));
        }
    }
}
