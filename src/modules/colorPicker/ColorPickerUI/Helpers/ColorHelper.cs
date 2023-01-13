// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        internal static (double Red, double Green, double Blue) ConvertToDouble(Color color)
            => (color.R / 255d, color.G / 255d, color.B / 255d);
    }
}
