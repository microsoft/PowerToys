// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// Helper class to easier work with colors
    /// </summary>
    internal static class ColorHelper
    {
        /// <summary>
        /// Convert a given <see cref="Color"/> to a CMYK color (cyan, magenta, yellow, black key)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The cyan[0..1], magenta[0..1], yellow[0..1] and black key[0..1] of the converted color</returns>
        internal static (double cyan, double magenta, double yellow, double blackKey) ConvertToCMYKColor(Color color)
        {
            // special case for black (avoid division by zero)
            if (color.R == 0 && color.G == 0 && color.B == 0)
            {
                return (0d, 0d, 0d, 1d);
            }

            var red = color.R / 255d;
            var green = color.G / 255d;
            var blue = color.B / 255d;

            var blackKey = 1d - Math.Max(Math.Max(red, green), blue);

            // special case for black (avoid division by zero)
            if (1d - blackKey == 0d)
            {
                return (0d, 0d, 0d, 1d);
            }

            var cyan = (1d - red - blackKey) / (1d - blackKey);
            var magenta = (1d - green - blackKey) / (1d - blackKey);
            var yellow = (1d - blue - blackKey) / (1d - blackKey);

            return (cyan, magenta, yellow, blackKey);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSB color (hue, saturation, brightness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and brightness [0..1] of the converted color</returns>
        internal static (double hue, double saturation, double brightness) ConvertToHSBColor(Color color)
            => (color.GetHue(), color.GetSaturation(), color.GetBrightness());

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSI color (hue, saturation, intensity)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and intensity [0..1] of the converted color</returns>
        internal static (double hue, double saturation, double intensity) ConvertToHSIColor(Color color)
        {
            // special case for black
            if (color.R == 0 && color.G == 0 && color.B == 0)
            {
                return (0d, 0d, 0d);
            }

            var red = color.R / 255d;
            var green = color.G / 255d;
            var blue = color.B / 255d;

            var intensity = (red + green + blue) / 3d;

            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;

            return (color.GetHue(), 1d - (min / intensity), intensity);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSL color (hue, saturation, lightness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and lightness [0..1] values of the converted color</returns>
        internal static (double hue, double saturation, double lightness) ConvertToHSLColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            var lightness = (max + min) / 2d;

            if (lightness == 0d || min == max)
            {
                return (color.GetHue(), 0d, lightness);
            }
            else if (lightness > 0d && lightness <= 0.5d)
            {
                return (color.GetHue(), (max - min) / (max + min), lightness);
            }

            return (color.GetHue(), (max - min) / (2d - (max + min)), lightness);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSV color (hue, saturation, value)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and value [0..1] of the converted color</returns>
        internal static (double hue, double saturation, double value) ConvertToHSVColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            return (color.GetHue(), max == 0d ? 0d : (max - min) / max, max);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HWB color (hue, whiteness, blackness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], whiteness [0..1] and blackness [0..1] of the converted color</returns>
        internal static (double hue, double whiteness, double blackness) ConvertToHWBColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            return (color.GetHue(), min, 1 - max);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a natural color (hue, whiteness, blackness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue, whiteness [0..1] and blackness [0..1] of the converted color</returns>
        internal static (string hue, double whiteness, double blackness) ConvertToNaturalColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            return (GetNaturalColorFromHue(color.GetHue()), min, 1 - max);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a CIE LAB color (LAB)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The lightness [0..100] and two chromaticities [-128..127]</returns>
        internal static (double lightness, double chromaticityA, double chromaticityB) ConvertToCIELABColor(Color color)
        {
            var xyz = ConvertToCIEXYZColor(color);
            var lab = GetCIELABColorFromCIEXYZ(xyz.x, xyz.y, xyz.z);

            return lab;
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a CIE XYZ color (XYZ)
        /// The constants of the formula used come from this wikipedia page:
        /// https://en.wikipedia.org/wiki/SRGB#The_reverse_transformation_(sRGB_to_CIE_XYZ)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The X [0..1], Y [0..1] and Z [0..1]</returns>
        internal static (double x, double y, double z) ConvertToCIEXYZColor(Color color)
        {
            double r = color.R / 255d;
            double g = color.G / 255d;
            double b = color.B / 255d;

            // inverse companding, gamma correction must be undone
            double rLinear = (r > 0.04045) ? Math.Pow((r + 0.055) / 1.055, 2.4) : (r / 12.92);
            double gLinear = (g > 0.04045) ? Math.Pow((g + 0.055) / 1.055, 2.4) : (g / 12.92);
            double bLinear = (b > 0.04045) ? Math.Pow((b + 0.055) / 1.055, 2.4) : (b / 12.92);

            return (
                (rLinear * 0.4124) + (gLinear * 0.3576) + (bLinear * 0.1805),
                (rLinear * 0.2126) + (gLinear * 0.7152) + (bLinear * 0.0722),
                (rLinear * 0.0193) + (gLinear * 0.1192) + (bLinear * 0.9505)
            );
        }

        /// <summary>
        /// Convert a CIE XYZ color <see cref="double"/> to a CIE LAB color (LAB)
        /// The constants of the formula used come from this wikipedia page:
        /// https://en.wikipedia.org/wiki/CIELAB_color_space#Converting_between_CIELAB_and_CIEXYZ_coordinates
        /// </summary>
        /// <param name="x">The <see cref="x"/> represents a mix of the three CIE RGB curves</param>
        /// <param name="y">The <see cref="y"/> represents the luminance</param>
        /// <param name="z">The <see cref="z"/> is quasi-equal to blue (of CIE RGB)</param>
        /// <returns>The lightness [0..100] and two chromaticities [-128..127]</returns>
        private static (double lightness, double chromaticityA, double chromaticityB)
            GetCIELABColorFromCIEXYZ(double x, double y, double z)
        {
            // These values are based on the D65 Illuminant
            x = x * 100 / 95.0489;
            y = y * 100 / 100.0;
            z = z * 100 / 108.8840;

            // XYZ to CIELab transformation
            double delta = 6d / 29;
            double m = (1d / 3) * Math.Pow(delta, -2);
            double t = Math.Pow(delta, 3);

            double fx = (x > t) ? Math.Pow(x, 1.0 / 3.0) : (x * m) + (16.0 / 116.0);
            double fy = (y > t) ? Math.Pow(y, 1.0 / 3.0) : (y * m) + (16.0 / 116.0);
            double fz = (z > t) ? Math.Pow(z, 1.0 / 3.0) : (z * m) + (16.0 / 116.0);

            double l = (116 * fy) - 16;
            double a = 500 * (fx - fy);
            double b = 200 * (fy - fz);

            return (l, a, b);
        }

        /// <summary>
        /// Return the natural color for the given hue value
        /// </summary>
        /// <param name="hue">The hue value to convert</param>
        /// <returns>A natural color</returns>
        private static string GetNaturalColorFromHue(double hue)
        {
            if (hue < 60d)
            {
                return $"R{Math.Round(hue / 0.6d, 0)}";
            }

            if (hue < 120d)
            {
                return $"Y{Math.Round((hue - 60d) / 0.6d, 0)}";
            }

            if (hue < 180d)
            {
                return $"G{Math.Round((hue - 120d) / 0.6d, 0)}";
            }

            if (hue < 240d)
            {
                return $"C{Math.Round((hue - 180d) / 0.6d, 0)}";
            }

            if (hue < 300d)
            {
                return $"B{Math.Round((hue - 240d) / 0.6d, 0)}";
            }

            return $"M{Math.Round((hue - 300d) / 0.6d, 0)}";
        }
    }
}
