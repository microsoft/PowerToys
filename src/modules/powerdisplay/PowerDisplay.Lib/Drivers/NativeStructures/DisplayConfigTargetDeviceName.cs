// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration target device name
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        /// <summary>
        /// Monitor friendly name - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort MonitorFriendlyDeviceName[64];

        /// <summary>
        /// Monitor device path - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort MonitorDevicePath[128];

        /// <summary>
        /// Helper method to get monitor friendly name as string
        /// </summary>
        public readonly string GetMonitorFriendlyDeviceName()
        {
            fixed (ushort* ptr = MonitorFriendlyDeviceName)
            {
                return new string((char*)ptr);
            }
        }

        /// <summary>
        /// Helper method to get monitor device path as string
        /// </summary>
        public readonly string GetMonitorDevicePath()
        {
            fixed (ushort* ptr = MonitorDevicePath)
            {
                return new string((char*)ptr);
            }
        }
    }
}
