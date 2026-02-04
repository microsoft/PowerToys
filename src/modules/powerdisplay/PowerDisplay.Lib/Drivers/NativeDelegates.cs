// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers;

/// <summary>
/// Native delegate type definitions
/// </summary>
public static class NativeDelegates
{
    /// <summary>
    /// Monitor enumeration procedure delegate
    /// </summary>
    /// <param name="hMonitor">Monitor handle</param>
    /// <param name="hdcMonitor">Monitor device context</param>
    /// <param name="lprcMonitor">Pointer to monitor rectangle</param>
    /// <param name="dwData">User data</param>
    /// <returns>True to continue enumeration</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
}
