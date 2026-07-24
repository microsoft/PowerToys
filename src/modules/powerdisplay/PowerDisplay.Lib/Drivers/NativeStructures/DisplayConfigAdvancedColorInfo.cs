// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Managed layout for DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigAdvancedColorInfo
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;

        public readonly bool AdvancedColorSupported => (Value & 0x1) != 0;

        public readonly bool AdvancedColorEnabled => (Value & 0x2) != 0;
    }
}
