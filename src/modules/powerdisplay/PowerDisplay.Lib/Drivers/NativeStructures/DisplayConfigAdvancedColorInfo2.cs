// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Managed layout for DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 (Windows 11).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigAdvancedColorInfo2
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;
        public uint ActiveColorMode;

        public readonly bool HighDynamicRangeSupported => (Value & 0x10) != 0;

        public readonly bool HighDynamicRangeUserEnabled => (Value & 0x20) != 0;

        public readonly bool IsHdrActive => ActiveColorMode == 2;
    }
}
