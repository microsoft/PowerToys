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
        /// Convert a given <see cref="Color"/> to a float color styling(0.1f, 0.1f, 0.1f)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The int / 255d for each value to get value between 0 and 1</returns>
        internal static (double red, double green, double blue) ConvertToDouble(Color color)
            => (color.R / 255d, color.G / 255d, color.B / 255d);

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
