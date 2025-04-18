// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProcessMonitorExtension;

internal sealed partial class ProcessListPage : ListPage
{
    public ProcessListPage()
    {
        this.Icon = new IconInfo("\ue9d9");
        this.Name = "Process Monitor";
    }

    public override IListItem[] GetItems() => DoGetItems();

    internal void UpdateItems() => this.RaiseItemsChanged(-1);

    private IListItem[] DoGetItems()
    {
        var items = GetRunningProcesses();
        this.IsLoading = false;
        var s = items
            .OrderByDescending(p => p.Memory)
            .Select((process) => new ListItem(new SwitchToProcess(process))
            {
                Title = process.Name,
                Subtitle = $"PID: {process.ProcessId}",
                MoreCommands = [
                    new CommandContextItem(new TerminateProcess(process, this))
                ],
            }).ToArray();
        return s;
    }

    private static IEnumerable<ProcessItem> GetRunningProcesses()
    {
        return Process.GetProcesses()
            .Select(p =>
            {
                var exePath = string.Empty;

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
