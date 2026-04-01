// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Physical monitor structure for DDC/CI
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct PhysicalMonitor
    {
        /// <summary>
        /// Physical monitor handle
        /// </summary>
        public IntPtr HPhysicalMonitor;

        /// <summary>
        /// Physical monitor description string - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort SzPhysicalMonitorDescription[128];

        /// <summary>
        /// Helper method to get description as string
        /// </summary>
        public readonly string GetDescription()
        {
            fixed (ushort* ptr = SzPhysicalMonitorDescription)
            {
                return new string((char*)ptr);
            }
        }
    }
}
