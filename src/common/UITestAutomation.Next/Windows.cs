// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Static helpers for discovering and attaching to windows that aren't the test's primary scope.
/// </summary>
/// <remarks>
/// Most tests target one module's main window (handled by <see cref="UITestBase"/> + <see cref="SessionHelper"/>).
/// But scenarios like "send the ColorPicker hotkey and assert the Editor pops up" need to discover
/// a brand-new window that may not exist when the test starts. These helpers enumerate top-level
/// windows via pure Win32 <c>EnumWindows</c> (<see cref="WindowControl.EnumerateProcessWindows"/>) —
/// no UIA, so they never attach a UIA client or disturb a window mid screen-capture — and find/wait
/// for those windows by process, title, class, or size.
/// </remarks>
public static class WindowsFinder
{
    public sealed record WindowInfo(long Hwnd, string Title, string ProcessName, int ProcessId, string ClassName, int Width, int Height);

    /// <summary>
    /// List all visible top-level windows via the pure Win32 <c>EnumWindows</c> API (no UIA). Includes
    /// windows with no Win32 title (e.g. the ColorPicker overlay/editor) — unlike winappcli's unfiltered
    /// <c>list-windows</c>, which drops them. <see cref="WindowInfo.Title"/> is the Win32 title, so match
    /// title-less windows by class or size (or use a process-scoped <see cref="Session"/> for a UIA Name).
    /// </summary>
    public static IReadOnlyList<WindowInfo> ListAll() => ToWindowInfos(WindowControl.EnumerateAllWindows());

    /// <summary>
    /// List visible top-level windows belonging to <paramref name="appNameOrPid"/> (process-name substring
    /// or PID), via pure Win32 <c>EnumWindows</c> filtered to the resolved process ids — no UIA. Includes
    /// title-less windows (the ColorPicker overlay/editor), matched by size/class.
    /// </summary>
    public static IReadOnlyList<WindowInfo> ListByApp(string appNameOrPid)
    {
        var pids = ResolveProcessIds(appNameOrPid);
        return pids.Count == 0
            ? Array.Empty<WindowInfo>()
            : ToWindowInfos(WindowControl.EnumerateProcessWindows(pids));
    }

    /// <summary>
    /// Map the raw Win32 windows to <see cref="WindowInfo"/>, keeping only real on-screen windows
    /// (visible with a non-empty rect) — matching winappcli's "app windows" semantics and filtering out
    /// the hidden helper / message-only windows that <c>EnumWindows</c> also returns.
    /// </summary>
    private static IReadOnlyList<WindowInfo> ToWindowInfos(IReadOnlyList<WindowControl.ProcessWindow> windows)
    {
        var names = new Dictionary<int, string>();
        var list = new List<WindowInfo>(windows.Count);
        foreach (var w in windows)
        {
            if (!w.IsVisible || w.Width <= 0 || w.Height <= 0)
            {
                continue;
            }

            list.Add(new WindowInfo(
                Hwnd: w.Hwnd.ToInt64(),
                Title: w.Title,
                ProcessName: ProcessNameForPid(w.ProcessId, names),
                ProcessId: w.ProcessId,
                ClassName: w.ClassName,
                Width: w.Width,
                Height: w.Height));
        }

        return list;
    }

    /// <summary>
    /// Resolve <paramref name="appNameOrPid"/> (a PID string, or a process-name substring like winappcli's
    /// <c>-a</c>) to the matching process ids. Best-effort — inaccessible processes are skipped.
    /// </summary>
    private static List<int> ResolveProcessIds(string appNameOrPid)
    {
        if (int.TryParse(appNameOrPid, out var pid))
        {
            return new List<int> { pid };
        }

        var needle = appNameOrPid.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appNameOrPid[..^4]
            : appNameOrPid;

        var ids = new List<int>();
        Process[] procs;
        try
        {
            procs = Process.GetProcesses();
        }
        catch
        {
            return ids;
        }

        foreach (var p in procs)
        {
            try
            {
                if (p.ProcessName.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(p.Id);
                }
            }
            catch
            {
                // Process exited or is inaccessible — skip.
            }
            finally
            {
                p.Dispose();
            }
        }

        return ids;
    }

    private static string ProcessNameForPid(int pid, Dictionary<int, string> cache)
    {
        if (cache.TryGetValue(pid, out var name))
        {
            return name;
        }

        try
        {
            using var p = Process.GetProcessById(pid);
            name = p.ProcessName;
        }
        catch
        {
            name = string.Empty;
        }

        cache[pid] = name;
        return name;
    }

    /// <summary>
    /// Poll until a window matching <paramref name="predicate"/> appears, or <paramref name="timeoutMS"/>
    /// elapses. Returns the window's <see cref="Session"/> wrapper on success.
    /// </summary>
    public static Session? WaitForWindow(Func<WindowInfo, bool> predicate, PowerToysModule attributeAs = PowerToysModule.Runner, int timeoutMS = 10_000, int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var w in ListAll())
            {
                Debug.WriteLine(w.ToString());
                if (predicate(w))
                {
                    return new Session(attributeAs, w.Hwnd, w.Title, w.ProcessId, w.ProcessName);
                }
            }

            Thread.Sleep(pollIntervalMS);
        }

        return null;
    }

    /// <summary>Convenience wrapper: wait for a window with the given title substring.</summary>
    public static Session? WaitForWindowByTitle(string titleContains, int timeoutMS = 10_000)
        => WaitForWindow(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase), timeoutMS: timeoutMS);

    /// <summary>
    /// Wait for any window owned by a process whose name contains <paramref name="processNameContains"/>.
    /// Enumerates the process's visible top-level windows via Win32 <c>EnumWindows</c>, so untitled
    /// windows (e.g. the ColorPicker overlay/editor) are discoverable by class/size.
    /// </summary>
    public static Session? WaitForWindowByProcess(string processNameContains, int timeoutMS = 10_000, int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var w in ListByApp(processNameContains))
            {
                Debug.WriteLine(w.ToString());
                return new Session(PowerToysModule.Runner, w.Hwnd, w.Title, w.ProcessId, w.ProcessName);
            }

            Thread.Sleep(pollIntervalMS);
        }

        return null;
    }

    /// <summary>
    /// Same as <see cref="WaitForWindowByProcess"/> but filters with <paramref name="predicate"/>.
    /// Use when the same process owns multiple windows (e.g. ColorPickerUI exposes both the
    /// small picker overlay and the larger editor window).
    /// </summary>
    public static Session? WaitForWindowByApp(
        string appNameOrPid,
        Func<WindowInfo, bool> predicate,
        int timeoutMS = 10_000,
        int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var w in ListByApp(appNameOrPid))
            {
                Debug.WriteLine(w.ToString());
                if (predicate(w))
                {
                    return new Session(PowerToysModule.Runner, w.Hwnd, w.Title, w.ProcessId, w.ProcessName);
                }
            }

            Thread.Sleep(pollIntervalMS);
        }

        return null;
    }
}
