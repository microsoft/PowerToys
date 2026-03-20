// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using TaskbarMonitor;

TaskbarPoller.SetDpiAwareness();

Console.OutputEncoding = System.Text.Encoding.UTF8;

var poller = new TaskbarPoller();
List<TaskbarSnapshot>? previous = null;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let finally block run
    TaskbarView.LeaveAlternateScreen();
    Environment.Exit(0);
};

Console.WriteLine("Monitoring taskbar changes. Press Ctrl+C to exit.");

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
    // TaskbarView.LeaveAlternateScreen();
    Console.WriteLine("Have a nice day!");
}
