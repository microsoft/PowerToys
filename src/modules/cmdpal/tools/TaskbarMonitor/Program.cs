// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using TaskbarMonitor;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

TaskbarPoller.SetDpiAwareness();

Console.OutputEncoding = System.Text.Encoding.UTF8;

var jsonMode = args.Contains("--json");
var debugMode = args.Contains("--debug");
var hwndMode = args.Contains("--hwnd");
var inputSiteMode = args.Contains("--inputsite");

using var poller = new TaskbarPoller();

if (hwndMode)
{
    HwndTest.Run();
    return;
}

if (inputSiteMode)
{
    unsafe
    {
        var shellTray = PInvoke.FindWindow("Shell_TrayWnd", null);
        Console.WriteLine($"Shell_TrayWnd = 0x{(nint)shellTray.Value:X}");

        // Check various HWND children
        var inputSite = PInvoke.FindWindowEx(shellTray, HWND.Null, "Windows.UI.Input.InputSite.WindowClass", null);
        Console.WriteLine($"InputSite (direct child) = 0x{(nint)inputSite.Value:X}");

        var rebar = PInvoke.FindWindowEx(shellTray, HWND.Null, "ReBarWindow32", null);
        Console.WriteLine($"ReBarWindow32 = 0x{(nint)rebar.Value:X}");

        var taskSw = PInvoke.FindWindowEx(rebar, HWND.Null, "MSTaskSwWClass", null);
        Console.WriteLine($"MSTaskSwWClass = 0x{(nint)taskSw.Value:X}");

        var taskList = PInvoke.FindWindowEx(taskSw, HWND.Null, "MSTaskListWClass", null);
        Console.WriteLine($"MSTaskListWClass = 0x{(nint)taskList.Value:X}");

        // Check for InputSite deeper in hierarchy
        var isInRebar = PInvoke.FindWindowEx(rebar, HWND.Null, "Windows.UI.Input.InputSite.WindowClass", null);
        Console.WriteLine($"InputSite (in ReBar) = 0x{(nint)isInRebar.Value:X}");

        var isInTaskSw = PInvoke.FindWindowEx(taskSw, HWND.Null, "Windows.UI.Input.InputSite.WindowClass", null);
        Console.WriteLine($"InputSite (in MSTaskSwWClass) = 0x{(nint)isInTaskSw.Value:X}");

        var isInTaskList = PInvoke.FindWindowEx(taskList, HWND.Null, "Windows.UI.Input.InputSite.WindowClass", null);
        Console.WriteLine($"InputSite (in MSTaskListWClass) = 0x{(nint)isInTaskList.Value:X}");

        // List ALL direct children of Shell_TrayWnd
        Console.WriteLine($"\nAll HWND children of Shell_TrayWnd:");
        var child = HWND.Null;
        Span<char> buf = stackalloc char[256];
        while (true)
        {
            child = PInvoke.FindWindowEx(shellTray, child, null as string, null);
            if (child.IsNull)
            {
                break;
            }

            PInvoke.GetClassName(child, buf);
            var cn = new string(buf.TrimEnd('\0'));
            PInvoke.GetWindowRect(child, out var cr);
            Console.WriteLine($"  0x{(nint)child.Value:X} class={cn} rect=({cr.left},{cr.top},{cr.right},{cr.bottom}) {cr.Width}x{cr.Height}");
        }
    }

    // Use TaskbarMetrics
    var metrics = new Microsoft.CmdPal.UI.Utilities.TaskbarMetrics();
    metrics.Update();
    Console.WriteLine($"\nTaskbarMetrics (InputSite scope):");
    Console.WriteLine($"  ButtonsWidthInPixels = {metrics.ButtonsWidthInPixels}");
    Console.WriteLine($"  TrayWidthInPixels    = {metrics.TrayWidthInPixels}");
    Console.WriteLine($"  ButtonCount          = {metrics.ButtonCount}");

    var snapshots2 = poller.PollAll();
    var primary = snapshots2.FirstOrDefault(s => s.IsPrimary);
    Console.WriteLine($"\nTaskbarPoller (Shell_TrayWnd scope):");
    Console.WriteLine($"  ButtonsWidth = {primary?.ButtonsWidth}");
    Console.WriteLine($"  TrayWidth    = {primary?.TrayWidth}");
    Console.WriteLine($"  ButtonCount  = {primary?.ButtonCount}");
    metrics.Dispose();
    return;
}

if (jsonMode || debugMode)
{
    var snapshots = poller.PollAll(debugMode ? Console.Error : null);
    if (jsonMode)
    {
        Console.WriteLine(JsonSerializer.Serialize(snapshots, AppJsonContext.Default.ListTaskbarSnapshot));
    }
    else
    {
        foreach (var s in snapshots)
        {
            Console.WriteLine(s);
        }
    }

    return;
}

// Event-driven mode: use SetWinEventHook to detect taskbar changes,
// then re-poll only when something changes.
List<TaskbarSnapshot>? previous = null;
var dirty = true; // poll once on startup

using var watcher = new TaskbarWatcher();
var threadId = PInvoke.GetCurrentThreadId();

watcher.Changed += () =>
{
    dirty = true;

    // Wake the message pump so it processes the change
    PInvoke.PostThreadMessage(threadId, PInvoke.WM_NULL, default, default);
};

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    TaskbarView.LeaveAlternateScreen();
    Environment.Exit(0);
};

TaskbarView.EnterAlternateScreen();

// Initial render
var snapshot = poller.PollAll();
TaskbarView.Render(snapshot, previous);
previous = snapshot;
dirty = false;

try
{
    // Win32 message pump — required for SetWinEventHook with
    // WINEVENT_OUTOFCONTEXT. GetMessage blocks until a message arrives,
    // so we burn zero CPU while idle.
    MSG msg;
    while (PInvoke.GetMessage(out msg, HWND.Null, 0, 0))
    {
        PInvoke.TranslateMessage(in msg);
        PInvoke.DispatchMessage(in msg);

        if (dirty)
        {
            dirty = false;
            snapshot = poller.PollAll();

            // When the user right-clicks the taskbar, UIA momentarily
            // reports 0 children. Skip these transient error snapshots
            // and keep showing the previous valid data.
            if (snapshot.Any(s => s.IsBottom && s.ButtonCount == 0))
            {
                continue;
            }

            TaskbarView.Render(snapshot, previous);
            previous = snapshot;
        }
    }
}
finally
{
    TaskbarView.LeaveAlternateScreen();
}
