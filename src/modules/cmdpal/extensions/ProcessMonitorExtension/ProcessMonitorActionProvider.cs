// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using System.Runtime.InteropServices;

namespace ProcessMonitorExtension;


internal sealed class ProcessItem
{
    internal Process Process { get; init; }
    internal int ProcessId { get; init; }
    internal string Name { get; init; }
    internal string ExePath { get; init; }
    internal long Memory { get; init; }
    internal long CPU { get; init; }
}

sealed class TerminateProcess: InvokableCommand
{
    private readonly ProcessItem process;
    public TerminateProcess(ProcessItem process)
    {
        this.process = process;
        this.Icon = new("\ue74d");
        this.Name = "End task";
    }
    public override ActionResult Invoke()
    {
        var process = Process.GetProcessById(this.process.ProcessId);
        process.Kill();
        return ActionResult.KeepOpen();
    }
}

sealed class SwitchToProcess: InvokableCommand
{
    [DllImport("user32.dll")]
    public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    private readonly ProcessItem process;
    public SwitchToProcess(ProcessItem process)
    {
        this.process = process;
        this.Icon = new(process.ExePath == "" ? "\uE7B8" : process.ExePath);
        this.Name = "Switch to";
    }
    public override ActionResult Invoke()
    {
        SwitchToThisWindow(process.Process.MainWindowHandle, true);
        return ActionResult.KeepOpen();
    }
}

sealed class ProcessListPage : ListPage {

    public ProcessListPage()
    {
        this.Icon = new("\ue9d9");
        this.Name = "Process Monitor";
    }

    public override ISection[] GetItems()
    {
        return DoGetItems();
    }
    private ISection[] DoGetItems()
    {
        var items = GetRunningProcesses();
        this.Loading = false;
        var s = new ListSection()
        {
            Title = "Processes",
            Items = items
            .OrderByDescending(p => p.Memory)
            .Select((process) => new ListItem(new SwitchToProcess(process))
            {
                Title = process.Name,
                Subtitle = $"PID: {process.ProcessId}",
                MoreCommands = [
                    new CommandContextItem(new TerminateProcess(process))
                ]
            }).ToArray()
        };
        return [ s ] ;
    }


    //internal void RefreshProcesses()
    //{
    //    Processes = GetRunningProcesses();
    //}

    private static IEnumerable<ProcessItem> GetRunningProcesses()
    {
        return Process.GetProcesses()
            .Select(p =>
            {
                var exePath = "";
                try
                {
                    exePath = p.MainModule.FileName;
                }
                catch
                {
                    // Handle cases where the icon extraction or file path retrieval fails
                }

                return new ProcessItem
                {
                    Process = p,
                    ProcessId = p.Id,
                    Name = p.ProcessName,
                    ExePath = exePath,
                    Memory = p.WorkingSet64,
                    // oh no CPU is not trivial to get
                };
            });
    }
}

internal sealed class ProcessMonitorActionProvider : ICommandProvider
{
    public string DisplayName => "Process Monitor Commands";
    public IconDataType Icon => new(""); // Optionally provide an icon URL

    public void Dispose() { }

    private readonly IListItem[] _Actions = [
        new ListItem(new ProcessListPage()) { Title = "Process Manager", Subtitle = "Kill processes" },
    ];

    public IListItem[] TopLevelCommands()
    {
        return _Actions;
    }
}
