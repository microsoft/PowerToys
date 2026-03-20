// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using TaskbarMonitor;

TaskbarPoller.SetDpiAwareness();

Console.OutputEncoding = System.Text.Encoding.UTF8;

var jsonMode = args.Contains("--json");
var debugMode = args.Contains("--debug");

using var poller = new TaskbarPoller();

if (jsonMode || debugMode)
{
    // Single-shot: poll once and output
    var snapshots = poller.PollAll(debugMode ? Console.Error : null);
    if (jsonMode)
    {
        Console.WriteLine(JsonSerializer.Serialize(snapshots, AppJsonContext.Default.ListTaskbarSnapshot));
    }
    else
    {
        // debug mode — the debug lines went to stderr, print results to stdout
        foreach (var s in snapshots)
        {
            Console.WriteLine(s);
        }
    }

    return;
}

List<TaskbarSnapshot>? previous = null;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    TaskbarView.LeaveAlternateScreen();
    Environment.Exit(0);
};

TaskbarView.EnterAlternateScreen();

try
{
    while (true)
    {
        var snapshot = poller.PollAll();
        TaskbarView.Render(snapshot, previous);
        previous = snapshot;
        Thread.Sleep(500);
    }
}
catch (Exception ex)
{
    TaskbarView.LeaveAlternateScreen();
    Console.WriteLine($"An error occurred: {ex.Message}");
}
finally
{
    TaskbarView.LeaveAlternateScreen();
}
