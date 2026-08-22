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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

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

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

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
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint GaRoot = 2;
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusedWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public RECT CaretRectangle;
    }

    /// <summary>
    /// A top-level window discovered by <see cref="EnumerateProcessWindows"/> / <see cref="EnumerateAllWindows"/>:
    /// its native handle, owning process id, window class, title, size in physical pixels, and visibility.
    /// </summary>
    public readonly record struct ProcessWindow(IntPtr Hwnd, int ProcessId, string ClassName, string Title, int Width, int Height, bool IsVisible);

    /// <summary>Diagnostic details for the current foreground window.</summary>
    public readonly record struct ForegroundWindowInfo(IntPtr Hwnd, int ProcessId, string ProcessName, string ClassName, string Title, bool? IsElevated);

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

    /// <summary>
    /// Whether any top-level window of <paramref name="className"/> is currently visible.
    /// </summary>
    /// <remarks>
    /// Cheap enough to poll: it reads class names only, never window titles, so it avoids the
    /// cross-process <c>WM_GETTEXT</c> that <see cref="EnumerateAllWindows"/> performs per window.
    /// For a window that only appears briefly, prefer <see cref="WindowShowWatcher"/> — no poll can
    /// tell "never shown" apart from "shown between two samples".
    /// </remarks>
    public static bool IsAnyWindowOfClassVisible(string className) =>
        AnyWindowOfClass(className, requireVisible: true);

    /// <summary>Whether any top-level window of <paramref name="className"/> exists, visible or not.</summary>
    public static bool AnyWindowOfClassExists(string className) =>
        AnyWindowOfClass(className, requireVisible: false);

    private static bool AnyWindowOfClass(string className, bool requireVisible)
    {
        // EnumWindows rather than chained FindWindowEx calls: when several windows share a class (the
        // caller's product may pool and recycle them) the chained form is easy to get subtly wrong and
        // end up only ever inspecting the first match.
        var found = false;

        try
        {
            EnumWindows(
                (hWnd, _) =>
                {
                    try
                    {
                        if ((!requireVisible || IsWindowVisible(hWnd)) &&
                            GetWindowClassName(hWnd).Equals(className, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            return false;
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
            // Best-effort: report whatever was determined before the failure.
        }

        return found;
    }

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

    /// <summary>Send <c>WM_CLOSE</c> to one exact HWND and wait for it to be destroyed.</summary>
    public static bool TryCloseWindow(long hwnd, int timeoutMS = 5_000)
    {
        try
        {
            var handle = new IntPtr(hwnd);
            if (!IsWindow(handle))
            {
                return true;
            }

            PostMessageW(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsWindow(handle))
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return !IsWindow(handle);
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

    /// <summary>Return the current foreground window handle.</summary>
    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    /// <summary>Whether the root window under a screen point is the expected HWND.</summary>
    public static bool IsPointOwnedByWindow(IntPtr window, int x, int y)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var atPoint = WindowFromPoint(new POINT { X = x, Y = y });
            return atPoint != IntPtr.Zero && GetAncestor(atPoint, GaRoot) == window;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Open the context menu owned by the control that currently has focus in a foreground window.</summary>
    public static bool TryOpenContextMenuForFocusedControl(IntPtr ownerWindow)
    {
        if (ownerWindow == IntPtr.Zero || !IsWindow(ownerWindow) || !TryBringToForeground(ownerWindow))
        {
            return false;
        }

        var threadId = GetWindowThreadProcessId(ownerWindow, out _);
        var threadInfo = new GUITHREADINFO { Size = Marshal.SizeOf<GUITHREADINFO>() };
        var targetWindow = GetGUIThreadInfo(threadId, ref threadInfo) && threadInfo.FocusedWindow != IntPtr.Zero
            ? threadInfo.FocusedWindow
            : ownerWindow;
        return PostMessageW(targetWindow, WM_CONTEXTMENU, targetWindow, new IntPtr(-1));
    }

    /// <summary>Return process, class, title, and elevation details for the current foreground HWND.</summary>
    public static ForegroundWindowInfo GetForegroundWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return new ForegroundWindowInfo(IntPtr.Zero, 0, string.Empty, string.Empty, string.Empty, null);
        }

        var foregroundThreadId = GetWindowThreadProcessId(hwnd, out var processId);
        if (foregroundThreadId == 0)
        {
            processId = 0;
        }

        var processName = string.Empty;
        if (processId != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch
            {
            }
        }

        return new ForegroundWindowInfo(
            hwnd,
            (int)processId,
            processName,
            GetWindowClassName(hwnd),
            GetWindowTitle(hwnd),
            processId == 0 ? null : ElevationHelper.IsProcessElevated((int)processId));
    }

    /// <summary>Bring an HWND forward until it owns foreground for the requested consecutive samples.</summary>
    public static bool WaitForForeground(
        IntPtr hwnd,
        int timeoutMS = 5_000,
        int requiredConsecutiveMatches = 1,
        int pollIntervalMS = 100)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        return WaitHelper.WaitForStable(
            observe: GetForegroundWindow,
            isMatch: foreground => foreground == hwnd,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: requiredConsecutiveMatches,
            pollIntervalMS: pollIntervalMS,
            recover: _ => TryBringToForeground(hwnd)).Succeeded;
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

    /// <summary>
    /// Force-terminate every exact-name process tree and wait until no matching process remains.
    /// Unlike <see cref="TryKillProcessByName"/>, this also treats an already-absent process as success.
    /// </summary>
    public static bool TryKillProcessTreeByNameAndWait(string exactProcessName, int timeoutMS = 10_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exactProcessName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var processes = Process.GetProcessesByName(exactProcessName);
            if (processes.Length == 0)
            {
                return true;
            }

            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                    }
                }

                foreach (var process in processes)
                {
                    try
                    {
                        var remainingMS = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                        if (!process.WaitForExit(remainingMS))
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        var remainingProcesses = Process.GetProcessesByName(exactProcessName);
        try
        {
            return remainingProcesses.Length == 0;
        }
        finally
        {
            foreach (var process in remainingProcesses)
            {
                process.Dispose();
            }
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
