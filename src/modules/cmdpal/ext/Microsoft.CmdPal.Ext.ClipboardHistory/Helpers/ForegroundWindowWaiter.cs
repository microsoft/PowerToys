// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

/// <summary>
/// Waits until the foreground window belongs to a process other than Command Palette,
/// so that synthesized input (e.g. Ctrl+V) reaches the app the user wants to paste into.
/// </summary>
internal static class ForegroundWindowWaiter
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(10);

    internal static bool WaitForForegroundToLeaveCurrentProcess(out TimeSpan waited)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = WaitForForegroundToLeaveProcess(
            (uint)Environment.ProcessId,
            GetForegroundProcessId,
            () => stopwatch.Elapsed,
            Thread.Sleep,
            DefaultTimeout,
            DefaultPollInterval);
        waited = stopwatch.Elapsed;
        return result;
    }

    /// <returns>
    /// <see langword="true"/> once a foreground window owned by another process is observed;
    /// <see langword="false"/> if the timeout elapses first. A missing foreground window
    /// (<see langword="null"/> process id) is treated as "not ready yet".
    /// </returns>
    internal static bool WaitForForegroundToLeaveProcess(
        uint processId,
        Func<uint?> getForegroundProcessId,
        Func<TimeSpan> getElapsed,
        Action<TimeSpan> sleep,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        ArgumentNullException.ThrowIfNull(getForegroundProcessId);
        ArgumentNullException.ThrowIfNull(getElapsed);
        ArgumentNullException.ThrowIfNull(sleep);

        while (true)
        {
            if (getForegroundProcessId() is uint foregroundProcessId && foregroundProcessId != processId)
            {
                return true;
            }

            if (getElapsed() >= timeout)
            {
                return false;
            }

            sleep(pollInterval);
        }
    }

    private static uint? GetForegroundProcessId()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        return NativeMethods.GetWindowThreadProcessId(hwnd, out var processId) == 0 ? null : processId;
    }
}
