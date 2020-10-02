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
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255f;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255f;

            var lightness = (max + min) / 2f;

            if (lightness == 0f || min == max)
            {
                return (color.GetHue(), 0f, lightness);
            }
            else if (lightness is > 0f and <= 0.5f)
            {
                return (color.GetHue(), (max - min) / (max + min), lightness);
            }

            return (color.GetHue(), (max - min) / (2f - (max + min)), lightness);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> color to a HSV color (hue, saturation, value)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and value [0..1] of the converted color</returns>
        internal static (double hue, double saturation, double value) ConvertToHSVColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255f;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255f;

            return (color.GetHue(), max == 0f ? 0f : (max - min) / max, max);
        }
    }
}
