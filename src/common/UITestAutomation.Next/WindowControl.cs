// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Fault-tolerant window cleanup helpers. Every method swallows exceptions and returns a
/// boolean — they're designed for test <c>finally</c> blocks where a cleanup failure must
/// never mask the real test failure.
/// </summary>
/// <remarks>
/// winappcli has no <c>close</c> verb, so closing goes through Win32 <c>WM_CLOSE</c>
/// (graceful) with an optional process-kill fallback. Focus uses <c>SetForegroundWindow</c>
/// against the HWND that <see cref="WindowsFinder"/> already discovers.
/// </remarks>
public static class WindowControl
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const uint WM_CLOSE = 0x0010;
    private const int SW_RESTORE = 9;

    /// <summary>
    /// Send <c>WM_CLOSE</c> to every window owned by <paramref name="appNameOrPid"/> and wait
    /// up to <paramref name="timeoutMS"/> for them to disappear. Tolerant: returns false on
    /// any failure instead of throwing.
    /// </summary>
    public static bool TryCloseByApp(string appNameOrPid, int timeoutMS = 5_000)
    {
        try
        {
            var windows = WindowsFinder.ListByApp(appNameOrPid);
            if (windows.Count == 0)
            {
                return true; // nothing to close
            }

            foreach (var w in windows)
            {
                TryCloseHwnd(w.Hwnd);
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
            while (DateTime.UtcNow < deadline)
            {
                if (WindowsFinder.ListByApp(appNameOrPid).Count == 0)
                {
                    return true;
                }

                Thread.Sleep(150);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Send <c>WM_CLOSE</c> to every window matching <paramref name="predicate"/> on the
    /// process and wait for them to disappear. Use when one process owns several windows and
    /// only some should be closed (e.g. close the ColorPicker editor but leave the overlay).
    /// </summary>
    public static bool TryCloseByApp(string appNameOrPid, Func<WindowsFinder.WindowInfo, bool> predicate, int timeoutMS = 5_000)
    {
        try
        {
            var targets = WindowsFinder.ListByApp(appNameOrPid).Where(predicate).ToList();
            if (targets.Count == 0)
            {
                return true;
            }

            foreach (var w in targets)
            {
                TryCloseHwnd(w.Hwnd);
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
            while (DateTime.UtcNow < deadline)
            {
                if (!WindowsFinder.ListByApp(appNameOrPid).Any(predicate))
                {
                    return true;
                }

                Thread.Sleep(150);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bring the first window owned by <paramref name="appNameOrPid"/> to the foreground.
    /// If the window is minimized it's first restored. Tolerant.
    /// </summary>
    public static bool TryFocusByApp(string appNameOrPid)
    {
        try
        {
            var w = WindowsFinder.ListByApp(appNameOrPid).FirstOrDefault();
            if (w is null || w.Hwnd == 0)
            {
                return false;
            }

            var hwnd = new IntPtr(w.Hwnd);
            if (!IsWindow(hwnd))
            {
                return false;
            }

            ShowWindow(hwnd, SW_RESTORE);
            return SetForegroundWindow(hwnd);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleanup convenience: close every window of <paramref name="closeApp"/> (if any) and
    /// bring <paramref name="focusApp"/> to the foreground. Mirrors the pattern in the legacy
    /// <c>TestHelper.CleanupTest</c> (close target window → re-attach to Settings) but does
    /// not throw, so it's safe to call from a test <c>finally</c>.
    /// </summary>
    public static void SafeCloseAndFocus(string closeApp, string focusApp, int closeTimeoutMS = 5_000)
    {
        TryCloseByApp(closeApp, closeTimeoutMS);
        TryFocusByApp(focusApp);
    }

    /// <summary>
    /// Force-terminate every process whose name contains <paramref name="processNameContains"/>.
    /// Use only as a last resort when <see cref="TryCloseByApp(string, int)"/> failed and the
    /// module's window must be gone before the next test starts.
    /// </summary>
    public static bool TryKillProcess(string processNameContains)
    {
        try
        {
            var hits = Process.GetProcesses()
                .Where(p =>
                {
                    try
                    {
                        return p.ProcessName.Contains(processNameContains, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            foreach (var p in hits)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }
                finally
                {
                    p.Dispose();
                }
            }

            return hits.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCloseHwnd(long hwnd)
    {
        try
        {
            if (hwnd == 0)
            {
                return;
            }

            var handle = new IntPtr(hwnd);
            if (IsWindow(handle))
            {
                PostMessageW(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
