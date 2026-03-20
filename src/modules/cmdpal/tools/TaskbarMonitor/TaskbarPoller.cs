// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;

namespace TaskbarMonitor;

/// <summary>
/// Polls the Windows taskbar using only P/Invoke (no COM / UI Automation)
/// so the code is fully AOT-compatible.
/// Call <see cref="SetDpiAwareness"/> once at process start.
/// </summary>
public sealed class TaskbarPoller
{
    /// <summary>
    /// Makes this process per-monitor DPI aware so that
    /// <c>GetWindowRect</c> returns true physical pixels and
    /// <c>GetDpiForWindow</c> returns the correct per-monitor DPI.
    /// Must be called before any HWND / DPI work.
    /// </summary>
    public static void SetDpiAwareness()
    {
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (DPI_AWARENESS_CONTEXT)-4
        unsafe
        {
            PInvoke.SetProcessDpiAwarenessContext(
                new Windows.Win32.UI.HiDpi.DPI_AWARENESS_CONTEXT((void*)-4));
        }
    }

    /// <summary>
    /// Takes a snapshot of every taskbar (primary + secondary monitors).
    /// Returns an empty list when no taskbar is found.
    /// </summary>
    public List<TaskbarSnapshot> PollAll()
    {
        var results = new List<TaskbarSnapshot>();

        // Primary taskbar
        var primary = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (!primary.IsNull)
        {
            results.Add(SnapshotTaskbar(primary, isPrimary: true));
        }

        // Secondary taskbars (one per additional monitor when
        // "Show taskbar on all displays" is enabled)
        var secondary = HWND.Null;
        while (true)
        {
            secondary = PInvoke.FindWindowEx(
                HWND.Null, secondary, "Shell_SecondaryTrayWnd", null);

            if (secondary.IsNull)
            {
                break;
            }

            results.Add(SnapshotTaskbar(secondary, isPrimary: false));
        }

        return results;
    }

    private static TaskbarSnapshot SnapshotTaskbar(HWND taskbarHwnd, bool isPrimary)
    {
        PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
        var dpi = PInvoke.GetDpiForWindow(taskbarHwnd);
        var isBottom = taskbarRect.Width > taskbarRect.Height;

        int buttonsWidth = 0;
        int trayWidth = 0;
        int buttonCount = 0;

        if (isBottom)
        {
            buttonsWidth = MeasureTaskbarButtons(taskbarHwnd, out buttonCount);
            trayWidth = MeasureTray(taskbarHwnd);
        }

        return new TaskbarSnapshot
        {
            IsPrimary = isPrimary,
            TaskbarWidth = taskbarRect.Width,
            TaskbarHeight = taskbarRect.Height,
            IsBottom = isBottom,
            ButtonsWidth = buttonsWidth,
            TrayWidth = trayWidth,
            Dpi = dpi,
            ButtonCount = buttonCount,
        };
    }

    /// <summary>
    /// Measures the width of the taskbar button area by finding each
    /// top-level child window of MSTaskListWClass and computing the
    /// bounding extent.  Pure P/Invoke — no COM needed.
    /// </summary>
    private static int MeasureTaskbarButtons(HWND taskbarHwnd, out int buttonCount)
    {
        buttonCount = 0;

        // Primary: Shell_TrayWnd → ReBarWindow32 → MSTaskSwWClass → MSTaskListWClass
        // Secondary taskbars have a slightly different hierarchy; we try
        // the same child-class walk and fall back gracefully.
        var rebar = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "ReBarWindow32", null);
        if (rebar.IsNull)
        {
            return 0;
        }

        var taskSw = PInvoke.FindWindowEx(rebar, HWND.Null, "MSTaskSwWClass", null);
        if (taskSw.IsNull)
        {
            return 0;
        }

        var taskList = PInvoke.FindWindowEx(taskSw, HWND.Null, "MSTaskListWClass", null);
        if (taskList.IsNull)
        {
            return 0;
        }

        PInvoke.GetWindowRect(taskList, out var taskListRect);

        // Enumerate child windows by iterating with FindWindowEx.
        var maxRight = 0;
        var count = 0;
        var child = HWND.Null;

        while (true)
        {
            child = PInvoke.FindWindowEx(taskList, child, null as string, null);
            if (child.IsNull)
            {
                break;
            }

            PInvoke.GetWindowRect(child, out var childRect);
            if (childRect.right > maxRight)
            {
                maxRight = childRect.right;
            }

            count++;
        }

        buttonCount = count;

        if (maxRight <= taskListRect.left)
        {
            // No child windows or all offscreen — fall back to the task
            // list window width itself.
            return taskListRect.Width;
        }

        return maxRight - taskListRect.left;
    }

    private static int MeasureTray(HWND taskbarHwnd)
    {
        var tray = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "TrayNotifyWnd", null);
        if (tray.IsNull)
        {
            return 0;
        }

        PInvoke.GetWindowRect(tray, out var rect);
        return rect.Width;
    }
}
