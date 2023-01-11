// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;
using ColorPicker.Properties;
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
        internal static string GetStringRepresentationFromMediaColor(System.Windows.Media.Color color, string colorRepresentationType)
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
        internal static string GetStringRepresentation(Color color, string colorRepresentationType, string colorFormat)
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
        /// Return a <see cref="string"/> representation of a CMYK color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the CMYK color presentation</param>
        /// <returns>A <see cref="string"/> representation of a CMYK color</returns>
        private static string ColorToCMYK(Color color)
        {
            var (cyan, magenta, yellow, blackKey) = ColorFormatHelper.ConvertToCMYKColor(color);

            cyan = Math.Round(cyan * 100);
            magenta = Math.Round(magenta * 100);
            yellow = Math.Round(yellow * 100);
            blackKey = Math.Round(blackKey * 100);

            return $"cmyk({cyan.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {magenta.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {yellow.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {blackKey.ToString(CultureInfo.InvariantCulture)}%)";
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

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HSB color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSB color</returns>
        private static string ColorToHSB(Color color)
        {
            var (hue, saturation, brightness) = ColorFormatHelper.ConvertToHSBColor(color);

            hue = Math.Round(hue);
            saturation = Math.Round(saturation * 100);
            brightness = Math.Round(brightness * 100);

            return $"hsb({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {saturation.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {brightness.ToString(CultureInfo.InvariantCulture)}%)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation float color styling(0.1f, 0.1f, 0.1f)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>a string value (0.1f, 0.1f, 0.1f)</returns>
        private static string ColorToFloat(Color color)
        {
            var (red, green, blue) = ColorHelper.ConvertToDouble(color);
            const int precision = 2;
            const string floatFormat = "0.##";

            return $"({Math.Round(red, precision).ToString(floatFormat, CultureInfo.InvariantCulture)}f"
                 + $", {Math.Round(green, precision).ToString(floatFormat, CultureInfo.InvariantCulture)}f"
                 + $", {Math.Round(blue, precision).ToString(floatFormat, CultureInfo.InvariantCulture)}f, 1f)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation decimal color value
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>a string value number</returns>
        private static string ColorToDecimal(Color color)
        {
            return $"{(color.R * 65536) + (color.G * 256) + color.B}";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSI color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HSI color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSI color</returns>
        private static string ColorToHSI(Color color)
        {
            var (hue, saturation, intensity) = ColorFormatHelper.ConvertToHSIColor(color);

            hue = Math.Round(hue);
            saturation = Math.Round(saturation * 100);
            intensity = Math.Round(intensity * 100);

            return $"hsi({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {saturation.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {intensity.ToString(CultureInfo.InvariantCulture)}%)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSL color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HSL color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSL color</returns>
        private static string ColorToHSL(Color color)
        {
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(color);

            hue = Math.Round(hue);
            saturation = Math.Round(saturation * 100);
            lightness = Math.Round(lightness * 100);

            // Using InvariantCulture since this is used for color representation
            return $"hsl({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {saturation.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {lightness.ToString(CultureInfo.InvariantCulture)}%)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSV color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HSV color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSV color</returns>
        private static string ColorToHSV(Color color)
        {
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(color);

            hue = Math.Round(hue);
            saturation = Math.Round(saturation * 100);
            value = Math.Round(value * 100);

            // Using InvariantCulture since this is used for color representation
            return $"hsv({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {saturation.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {value.ToString(CultureInfo.InvariantCulture)}%)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a HWB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HWB color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HWB color</returns>
        private static string ColorToHWB(Color color)
        {
            var (hue, whiteness, blackness) = ColorFormatHelper.ConvertToHWBColor(color);

            hue = Math.Round(hue);
            whiteness = Math.Round(whiteness * 100);
            blackness = Math.Round(blackness * 100);

            return $"hwb({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {whiteness.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {blackness.ToString(CultureInfo.InvariantCulture)}%)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a natural color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the natural color presentation</param>
        /// <returns>A <see cref="string"/> representation of a natural color</returns>
        private static string ColorToNCol(Color color)
        {
            var (hue, whiteness, blackness) = ColorFormatHelper.ConvertToNaturalColor(color);

            whiteness = Math.Round(whiteness * 100);
            blackness = Math.Round(blackness * 100);

            return $"{hue}"
                 + $", {whiteness.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {blackness.ToString(CultureInfo.InvariantCulture)}%";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a RGB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the RGB color presentation</param>
        /// <returns>A <see cref="string"/> representation of a RGB color</returns>
        private static string ColorToRGB(Color color)
            => $"rgb({color.R.ToString(CultureInfo.InvariantCulture)}"
             + $", {color.G.ToString(CultureInfo.InvariantCulture)}"
             + $", {color.B.ToString(CultureInfo.InvariantCulture)})";

        /// <summary>
        /// Returns a <see cref="string"/> representation of a CIE LAB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the CIE LAB color presentation</param>
        /// <returns>A <see cref="string"/> representation of a CIE LAB color</returns>
        private static string ColorToCIELAB(Color color)
        {
            var (lightness, chromaticityA, chromaticityB) = ColorFormatHelper.ConvertToCIELABColor(color);
            lightness = Math.Round(lightness, 2);
            chromaticityA = Math.Round(chromaticityA, 2);
            chromaticityB = Math.Round(chromaticityB, 2);

            return $"CIELab({lightness.ToString(CultureInfo.InvariantCulture)}" +
                   $", {chromaticityA.ToString(CultureInfo.InvariantCulture)}" +
                   $", {chromaticityB.ToString(CultureInfo.InvariantCulture)})";
        }

        /// <summary>
        /// Returns a <see cref="string"/> representation of a CIE XYZ color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the CIE XYZ color presentation</param>
        /// <returns>A <see cref="string"/> representation of a CIE XYZ color</returns>
        private static string ColorToCIEXYZ(Color color)
        {
            var (x, y, z) = ColorFormatHelper.ConvertToCIEXYZColor(color);

            x = Math.Round(x * 100, 4);
            y = Math.Round(y * 100, 4);
            z = Math.Round(z * 100, 4);

            return $"XYZ({x.ToString(CultureInfo.InvariantCulture)}" +
                   $", {y.ToString(CultureInfo.InvariantCulture)}" +
                   $", {z.ToString(CultureInfo.InvariantCulture)})";
        }

        /// <summary>
        /// Return a hexadecimal integer <see cref="string"/> representation of a RGB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the hexadecimal integer presentation</param>
        /// <returns>A hexadecimal integer <see cref="string"/> representation of a RGB color</returns>
        private static string ColorToHexInteger(Color color)
        {
            const string hexFormat = "X2";

            return "0xFF"
                + $"{color.R.ToString(hexFormat, CultureInfo.InvariantCulture)}"
                + $"{color.G.ToString(hexFormat, CultureInfo.InvariantCulture)}"
                + $"{color.B.ToString(hexFormat, CultureInfo.InvariantCulture)}";
        }

        public static string GetColorNameFromColorIdentifier(string colorIdentifier)
        {
            switch (colorIdentifier)
            {
                case "TEXT_COLOR_WHITE": return Resources.TEXT_COLOR_WHITE;
                case "TEXT_COLOR_BLACK": return Resources.TEXT_COLOR_BLACK;
                case "TEXT_COLOR_LIGHTGRAY": return Resources.TEXT_COLOR_LIGHTGRAY;
                case "TEXT_COLOR_GRAY": return Resources.TEXT_COLOR_GRAY;
                case "TEXT_COLOR_DARKGRAY": return Resources.TEXT_COLOR_DARKGRAY;
                case "TEXT_COLOR_CORAL": return Resources.TEXT_COLOR_CORAL;
                case "TEXT_COLOR_ROSE": return Resources.TEXT_COLOR_ROSE;
                case "TEXT_COLOR_LIGHTORANGE": return Resources.TEXT_COLOR_LIGHTORANGE;
                case "TEXT_COLOR_TAN": return Resources.TEXT_COLOR_TAN;
                case "TEXT_COLOR_LIGHTYELLOW": return Resources.TEXT_COLOR_LIGHTYELLOW;
                case "TEXT_COLOR_LIGHTGREEN": return Resources.TEXT_COLOR_LIGHTGREEN;
                case "TEXT_COLOR_LIME": return Resources.TEXT_COLOR_LIME;
                case "TEXT_COLOR_AQUA": return Resources.TEXT_COLOR_AQUA;
                case "TEXT_COLOR_SKYBLUE": return Resources.TEXT_COLOR_SKYBLUE;
                case "TEXT_COLOR_LIGHTTURQUOISE": return Resources.TEXT_COLOR_LIGHTTURQUOISE;
                case "TEXT_COLOR_PALEBLUE": return Resources.TEXT_COLOR_PALEBLUE;
                case "TEXT_COLOR_LIGHTBLUE": return Resources.TEXT_COLOR_LIGHTBLUE;
                case "TEXT_COLOR_ICEBLUE": return Resources.TEXT_COLOR_ICEBLUE;
                case "TEXT_COLOR_PERIWINKLE": return Resources.TEXT_COLOR_PERIWINKLE;
                case "TEXT_COLOR_LAVENDER": return Resources.TEXT_COLOR_LAVENDER;
                case "TEXT_COLOR_PINK": return Resources.TEXT_COLOR_PINK;
                case "TEXT_COLOR_RED": return Resources.TEXT_COLOR_RED;
                case "TEXT_COLOR_ORANGE": return Resources.TEXT_COLOR_ORANGE;
                case "TEXT_COLOR_BROWN": return Resources.TEXT_COLOR_BROWN;
                case "TEXT_COLOR_GOLD": return Resources.TEXT_COLOR_GOLD;
                case "TEXT_COLOR_YELLOW": return Resources.TEXT_COLOR_YELLOW;
                case "TEXT_COLOR_OLIVEGREEN": return Resources.TEXT_COLOR_OLIVEGREEN;
                case "TEXT_COLOR_GREEN": return Resources.TEXT_COLOR_GREEN;
                case "TEXT_COLOR_BRIGHTGREEN": return Resources.TEXT_COLOR_BRIGHTGREEN;
                case "TEXT_COLOR_TEAL": return Resources.TEXT_COLOR_TEAL;
                case "TEXT_COLOR_TURQUOISE": return Resources.TEXT_COLOR_TURQUOISE;
                case "TEXT_COLOR_BLUE": return Resources.TEXT_COLOR_BLUE;
                case "TEXT_COLOR_BLUEGRAY": return Resources.TEXT_COLOR_BLUEGRAY;
                case "TEXT_COLOR_INDIGO": return Resources.TEXT_COLOR_INDIGO;
                case "TEXT_COLOR_PURPLE": return Resources.TEXT_COLOR_PURPLE;
                case "TEXT_COLOR_DARKRED": return Resources.TEXT_COLOR_DARKRED;
                case "TEXT_COLOR_DARKYELLOW": return Resources.TEXT_COLOR_DARKYELLOW;
                case "TEXT_COLOR_DARKGREEN": return Resources.TEXT_COLOR_DARKGREEN;
                case "TEXT_COLOR_DARKTEAL": return Resources.TEXT_COLOR_DARKTEAL;
                case "TEXT_COLOR_DARKBLUE": return Resources.TEXT_COLOR_DARKBLUE;
                case "TEXT_COLOR_DARKPURPLE": return Resources.TEXT_COLOR_DARKPURPLE;
                case "TEXT_COLOR_PLUM": return Resources.TEXT_COLOR_PLUM;
                default: return string.Empty;
            }
        }

        public static string ReplaceName(string colorFormat, Color color)
        {
            return colorFormat.Replace(ColorFormatHelper.GetColorNameParameter(), GetColorNameFromColorIdentifier(ColorNameHelper.GetColorNameIdentifier(color)));
        }
    }
}
