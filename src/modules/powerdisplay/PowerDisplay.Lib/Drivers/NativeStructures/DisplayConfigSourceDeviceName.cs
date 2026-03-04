// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration source device name - contains GDI device name (e.g., "\\.\DISPLAY1")
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        /// <summary>
        /// GDI device name - fixed buffer for 32 wide characters (CCHDEVICENAME)
        /// </summary>
        public fixed ushort ViewGdiDeviceName[32];

        /// <summary>
        /// Helper method to get GDI device name as string
        /// </summary>
        public readonly string GetViewGdiDeviceName()
        {
            fixed (ushort* ptr = ViewGdiDeviceName)
            {
                return new string((char*)ptr);
            }
        }
    }
}
