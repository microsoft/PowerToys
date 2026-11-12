// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace ColorPicker.Helpers;

internal static class WindowCaptureExclusionHelper
{
    // Windows 10 version 2004 (build 19041) is the minimum supported version. PowerToys
    // itself requires the same version, so this check is not strictly required, but is
    // useful as a safeguard.
    private static readonly bool IsSupported =
        Environment.OSVersion.Version >= new Version(10, 0, 19041);

    // Only logging once per session to avoid repeated identical warnings, as the zoom
    // window may be used very often.
    private static bool hasLoggedFailure;

    internal static bool Exclude(IntPtr hwnd) =>
        SetWindowAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);

    internal static bool Include(IntPtr hwnd) =>
        SetWindowAffinity(hwnd, NativeMethods.WDA_NONE);

    private static bool SetWindowAffinity(nint hwnd, uint affinity)
    {
        if (!IsSupported)
        {
            return false;
        }

        bool success = NativeMethods.SetWindowDisplayAffinity(hwnd, affinity);

        if (!success)
        {
            int errorCode = Marshal.GetLastWin32Error();
            if (!hasLoggedFailure)
            {
                Logger.LogWarning(
                    $"Failed to set window display affinity. Error code: {errorCode}");
                hasLoggedFailure = true;
            }
        }

        return success;
    }
}
