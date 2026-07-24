// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Request layout used by Windows Settings to commit the HDR SDR-white-level slider.
    /// The associated device-info type is private, so callers must capability-gate failures.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigSetSdrWhiteLevel
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint SdrWhiteLevel;
        public byte FinalValue;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
    }
}
