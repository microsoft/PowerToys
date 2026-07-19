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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint WM_CLOSE = 0x0010;
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// A top-level window discovered by <see cref="EnumerateProcessWindows"/> / <see cref="EnumerateAllWindows"/>:
    /// its native handle, owning process id, window class, title, size in physical pixels, and visibility.
    /// </summary>
    public readonly record struct ProcessWindow(IntPtr Hwnd, int ProcessId, string ClassName, string Title, int Width, int Height, bool IsVisible);

    /// <summary>
    /// Enumerate the top-level windows owned by any process in <paramref name="processIds"/> using the
    /// pure Win32 <c>EnumWindows</c> API. Unlike winappcli's UI-Automation-backed <c>list-windows</c>,
    /// this never attaches a UIA client or walks a window's UIA tree, so it is safe to call against a
    /// process that is mid screen-capture (e.g. the Measure Tool overlay) without disturbing it.
    /// </summary>
    public static IReadOnlyList<ProcessWindow> EnumerateProcessWindows(IReadOnlyCollection<int> processIds)
    {
        if (processIds.Count == 0)
        {
            return Array.Empty<ProcessWindow>();
        }

        return EnumerateTopLevelWindows(processIds.Contains);
    }

    /// <summary>
    /// Enumerate ALL top-level windows via the pure Win32 <c>EnumWindows</c> API (no process filter).
    /// Same no-UIA path as <see cref="EnumerateProcessWindows"/>.
    /// </summary>
    public static IReadOnlyList<ProcessWindow> EnumerateAllWindows() => EnumerateTopLevelWindows(null);

    private static IReadOnlyList<ProcessWindow> EnumerateTopLevelWindows(Func<int, bool>? pidFilter)
    {
        var result = new List<ProcessWindow>();

        try
        {
            EnumWindows(
                (hWnd, _) =>
                {
                    try
                    {
                        GetWindowThreadProcessId(hWnd, out var pid);
                        var pidInt = (int)pid;
                        if (pidFilter is null || pidFilter(pidInt))
                        {
                            var (width, height) = GetWindowSize(hWnd);
                            result.Add(new ProcessWindow(
                                hWnd,
                                pidInt,
                                GetWindowClassName(hWnd),
                                GetWindowTitle(hWnd),
                                width,
                                height,
                                IsWindowVisible(hWnd)));
                        }
                    }
                    catch
                    {
                        // Ignore any single window we can't read; keep enumerating.
                    }

                    return true;
                },
                IntPtr.Zero);
        }
        catch
        {
            // Best-effort: return whatever was collected before the failure.
        }

        return result;
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        var buffer = new char[256];
        var len = GetClassNameW(hWnd, buffer, buffer.Length);
        return len > 0 ? new string(buffer, 0, len) : string.Empty;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var buffer = new char[512];
        var len = GetWindowTextW(hWnd, buffer, buffer.Length);
        return len > 0 ? new string(buffer, 0, len) : string.Empty;
    }

    private static (int Width, int Height) GetWindowSize(IntPtr hWnd)
    {
        return GetWindowRect(hWnd, out var r)
            ? (Math.Max(0, r.Right - r.Left), Math.Max(0, r.Bottom - r.Top))
            : (0, 0);
    }

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
    /// <summary>
    /// Bring <paramref name="hwnd"/> to the foreground RELIABLY, defeating the Win32 foreground lock.
    /// A bare <c>SetForegroundWindow</c> from a process that isn't already the foreground is silently
    /// refused (SPI_SETFOREGROUNDLOCKTIMEOUT) — which is exactly what leaves a freshly-shown overlay /
    /// toolbar sitting BEHIND the window that held the foreground when it was triggered, so a coordinate
    /// click then lands on the covering window instead of the target. Briefly attaching our input queue
    /// to the current foreground thread lifts that restriction for the call. Best-effort; tolerant.
    /// </summary>
    public static bool TryBringToForeground(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                return false;
            }

            var foreground = GetForegroundWindow();
            if (foreground == hwnd)
            {
                // Already the foreground window — don't touch its show-state or the input queues.
                return true;
            }

            var foregroundThread = foreground == IntPtr.Zero ? 0u : GetWindowThreadProcessId(foreground, out _);
            var currentThread = GetCurrentThreadId();

            // Only un-MINIMIZE. Do NOT SW_RESTORE a maximized window — that un-maximizes it, resizing the
            // window and invalidating any element coordinates already resolved for the click (the cause of
            // the arm64 "Settings got resized then the toggle click missed" flakiness).
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            var attached = foregroundThread != 0 && foregroundThread != currentThread;
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, true);
            }

            BringWindowToTop(hwnd);
            var ok = SetForegroundWindow(hwnd);

            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }

            return ok;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryFocusByApp(string appNameOrPid)
    {
        try
        {
            var w = WindowsFinder.ListByApp(appNameOrPid).FirstOrDefault();
            if (w is null || w.Hwnd == 0)
            {
                return false;
            }

            return TryBringToForeground(new IntPtr(w.Hwnd));
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

    /// <summary>
    /// Force-terminate every process whose name <b>exactly</b> equals <paramref name="exactProcessName"/>
    /// (no extension, case-insensitive — the form <see cref="Process.GetProcessesByName(string)"/> accepts).
    /// Prefer this over <see cref="TryKillProcess"/> for short names like "PowerToys" that are a
    /// substring of unrelated processes (e.g. a "PowerToys.*.UITests" test host the run is executing
    /// in). Tolerant — returns false on any failure instead of throwing.
    /// </summary>
    public static bool TryKillProcessByName(string exactProcessName)
    {
        try
        {
            var hits = Process.GetProcessesByName(exactProcessName);
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

            return hits.Length > 0;
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
