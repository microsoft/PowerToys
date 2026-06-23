// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Windows.Win32.Foundation;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Display configuration mode information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigModeInfo
    {
        public uint InfoType;
        public uint Id;
        public LUID AdapterId;
        public DisplayConfigModeInfoUnion ModeInfo;
    }
}
