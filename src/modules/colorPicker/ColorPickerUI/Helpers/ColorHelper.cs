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
        /// Convert a given <see cref="Color"/> color to a HSL color (hue, saturation, lightness)
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
        /// Convert a given <see cref="Color"/> color to a HSV color (hue, saturation, value)
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
        /// Convert a given <see cref="Color"/> color to a CYMK color (cyan, magenta, yellow, black key)
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
    }
}
