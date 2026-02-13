// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Monitor information extended structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct MonitorInfoEx
    {
        /// <summary>
        /// Structure size
        /// </summary>
        public uint CbSize;

        /// <summary>
        /// Monitor rectangle area
        /// </summary>
        public Rect RcMonitor;

        /// <summary>
        /// Work area rectangle
        /// </summary>
        public Rect RcWork;

        /// <summary>
        /// Flags
        /// </summary>
        public uint DwFlags;

        /// <summary>
        /// Device name (e.g., "\\.\DISPLAY1") - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort SzDevice[32];

        /// <summary>
        /// Helper property to get device name as string
        /// </summary>
        public readonly string GetDeviceName()
        {
            fixed (ushort* ptr = SzDevice)
            {
                return new string((char*)ptr);
            }
        }
    }
}
