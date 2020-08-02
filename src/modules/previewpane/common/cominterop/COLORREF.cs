// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// The COLORREF value is used to specify an RGB color.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct COLORREF
    {
        /// <summary>
        /// Stores an RGB color value in a 32 bit integer.
        /// </summary>
        public uint Dword;

        /// <summary>
        /// Gets RGB value stored in <see cref="Dword"/> in <see cref="Color"/> structure.
        /// </summary>
        public Color Color
        {
            get
            {
                return Color.FromArgb(
                    (int)(0x000000FFU & this.Dword),
                    (int)(0x0000FF00U & this.Dword) >> 8,
                    (int)(0x00FF0000U & this.Dword) >> 16);
            }
        }
    }
}
