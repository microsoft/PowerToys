// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct COLORREF
    {
        public uint Dword;

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
