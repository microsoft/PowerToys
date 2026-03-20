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

using var poller = new TaskbarPoller();

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
            TaskbarView.Render(snapshot, previous);
            previous = snapshot;
        }
    }
}
finally
{
    TaskbarView.LeaveAlternateScreen();
}
