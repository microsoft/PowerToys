// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake.Core.Native;

internal static partial class SessionStateDetector
{
    private const int WtsCurrentSession = -1;
    private const int WtsSessionInfoEx = 25;

    [StructLayout(LayoutKind.Sequential)]
    private struct Wtsinfoex
    {
        public int Level;
        public int Reserved;
        public WtsinfoexLevel Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct WtsinfoexLevel
    {
        [FieldOffset(0)]
        public WtsinfoexLevel1 Level1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WtsinfoexLevel1
    {
        public int SessionId;
        public int SessionState;
        public int SessionFlags;
    }

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        int infoClass,
        out IntPtr ppBuffer,
        out int bytesReturned);

    [LibraryImport("wtsapi32.dll")]
    private static partial void WTSFreeMemory(IntPtr memory);

    public static bool IsWorkstationLocked()
    {
        if (!WTSQuerySessionInformation(
                IntPtr.Zero,
                WtsCurrentSession,
                WtsSessionInfoEx,
                out var buffer,
                out _))
        {
            // The session state is unknown. Do not assume the session is locked.
            return false;
        }

        try
        {
            var info = Marshal.PtrToStructure<Wtsinfoex>(buffer);

            return info.Data.Level1.SessionFlags == 0;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }
}
