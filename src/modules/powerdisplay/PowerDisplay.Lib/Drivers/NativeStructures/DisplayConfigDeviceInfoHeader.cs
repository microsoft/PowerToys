// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Windows.Win32.Foundation;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration device information header
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public LUID AdapterId;
        public uint Id;
    }
}
