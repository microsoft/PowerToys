// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// Helper class to easier work with color representation
    /// </summary>
    internal static class ColorRepresentationHelper
    {
        /// <summary>
        /// Return a <see cref="string"/> representation of a given <see cref="Color"/>
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the presentation</param>
        /// <param name="colorRepresentationType">The type of the representation</param>
        /// <returns>A <see cref="string"/> representation of a color</returns>
        internal static string GetStringRepresentationFromMediaColor(System.Windows.Media.Color color, ColorRepresentationType colorRepresentationType)
        {
            var drawingcolor = Color.FromArgb(color.A, color.R, color.G, color.B);
            return GetStringRepresentation(drawingcolor, colorRepresentationType);
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a given <see cref="Color"/>
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the presentation</param>
        /// <param name="colorRepresentationType">The type of the representation</param>
        /// <returns>A <see cref="string"/> representation of a color</returns>
        internal static string GetStringRepresentation(Color color, ColorRepresentationType colorRepresentationType)
            => colorRepresentationType switch
            {
                ColorRepresentationType.CMYK => ColorToCMYK(color),
                ColorRepresentationType.HEX => ColorToHex(color),
                ColorRepresentationType.HSB => ColorToHSB(color),
                ColorRepresentationType.HSI => ColorToHSI(color),
                ColorRepresentationType.HSL => ColorToHSL(color),
                ColorRepresentationType.HSV => ColorToHSV(color),
                ColorRepresentationType.HWB => ColorToHWB(color),
                ColorRepresentationType.NCol => ColorToNCol(color),
                ColorRepresentationType.RGB => ColorToRGB(color),
                ColorRepresentationType.CIELAB => ColorToCIELAB(color),
                ColorRepresentationType.CIEXYZ => ColorToCIEXYZ(color),
                ColorRepresentationType.VEC4 => ColorToFloat(color),
                ColorRepresentationType.DecimalValue => ColorToDecimal(color),

                // Fall-back value, when "_userSettings.CopiedColorRepresentation.Value" is incorrect
                _ => ColorToHex(color),
            };

        /// <summary>
        /// Return a <see cref="string"/> representation of a CMYK color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the CMYK color presentation</param>
        /// <returns>A <see cref="string"/> representation of a CMYK color</returns>
        private static string ColorToCMYK(Color color)
        {
            var (cyan, magenta, yellow, blackKey) = ColorHelper.ConvertToCMYKColor(color);

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
        /// <param name="color">The see cref="Color"/> for the hexadecimal presentation</param>
        /// <returns>A hexadecimal <see cref="string"/> representation of a RGB color</returns>
        private static string ColorToHex(Color color)
            => $"{color.R.ToString("x2", CultureInfo.InvariantCulture)}"
             + $"{color.G.ToString("x2", CultureInfo.InvariantCulture)}"
             + $"{color.B.ToString("x2", CultureInfo.InvariantCulture)}";

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSB color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HSB color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSB color</returns>
        private static string ColorToHSB(Color color)
        {
            var (hue, saturation, brightness) = ColorHelper.ConvertToHSBColor(color);

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
            var precision = 2;

            return $"({Math.Round(red, precision).ToString("0.##", CultureInfo.InvariantCulture)}f, {Math.Round(green, precision).ToString("0.##", CultureInfo.InvariantCulture)}f, {Math.Round(blue, precision).ToString("0.##", CultureInfo.InvariantCulture)}f, 1f)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation decimal color value
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>a string value number</returns>
        private static string ColorToDecimal(Color color)
        {
            return $"{color.R + (color.G * 256) + (color.B * 65536)}";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSI color
        /// </summary>
        /// <param name="color">The <see cref="Color"/> for the HSI color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSI color</returns>
        private static string ColorToHSI(Color color)
        {
            var (hue, saturation, intensity) = ColorHelper.ConvertToHSIColor(color);

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
            var (hue, saturation, lightness) = ColorHelper.ConvertToHSLColor(color);

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
            var (hue, saturation, value) = ColorHelper.ConvertToHSVColor(color);

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
            var (hue, whiteness, blackness) = ColorHelper.ConvertToHWBColor(color);

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
            var (hue, whiteness, blackness) = ColorHelper.ConvertToNaturalColor(color);

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
            var (lightness, chromaticityA, chromaticityB) = ColorHelper.ConvertToCIELABColor(color);
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
            var (x, y, z) = ColorHelper.ConvertToCIEXYZColor(color);

            x = Math.Round(x * 100, 4);
            y = Math.Round(y * 100, 4);
            z = Math.Round(z * 100, 4);

            return $"xyz({x.ToString(CultureInfo.InvariantCulture)}" +
                   $", {y.ToString(CultureInfo.InvariantCulture)}" +
                   $", {z.ToString(CultureInfo.InvariantCulture)})";
        }
    }
}
