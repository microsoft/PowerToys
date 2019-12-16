// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace FancyZonesEditor
{
    // PInvokes to handshake with fancyzones backend
    internal static class Native
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        internal delegate int PersistZoneSet(
            [MarshalAs(UnmanagedType.LPWStr)] string activeKey,
            [MarshalAs(UnmanagedType.LPWStr)] string resolutionKey,
            uint monitor,
            ushort layoutId,
            int zoneCount,
            [MarshalAs(UnmanagedType.LPArray)] int[] zoneArray);
    }
}
