// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// The RECT structure defines a rectangle by the coordinates of its upper-left and lower-right corners.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        /// <summary>
        /// Gets or sets specifies the x-coordinate of the upper-left corner of the rectangle.
        /// </summary>
        public int Left { get; set; }

        /// <summary>
        /// Gets or sets specifies the y-coordinate of the upper-left corner of the rectangle.
        /// </summary>
        public int Top { get; set; }

        /// <summary>
        /// Gets or sets specifies the x-coordinate of the lower-right corner of the rectangle.
        /// </summary>
        public int Right { get; set; }

        /// <summary>
        /// Gets or sets specifies the y-coordinate of the lower-right corner of the rectangle.
        /// </summary>
        public int Bottom { get; set; }

        /// <summary>
        /// Creates a <see cref="Rectangle" /> structure with the edge locations specified in the struct.
        /// </summary>
        /// <returns>Return a <see cref="Rectangle"/>.</returns>
        public Rectangle ToRectangle()
        {
            return Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }
    }
}
