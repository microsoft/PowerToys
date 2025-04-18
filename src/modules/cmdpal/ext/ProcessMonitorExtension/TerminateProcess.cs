// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProcessMonitorExtension;

internal sealed partial class TerminateProcess : InvokableCommand
{
    private readonly ProcessItem _process;
    private readonly ProcessListPage _owner;

    public TerminateProcess(ProcessItem process, ProcessListPage owner)
    {
        _process = process;
        _owner = owner;
        Icon = new IconInfo("\ue74d");
        Name = "End task";
    }

    public override CommandResult Invoke()
    {
        var process = Process.GetProcessById(_process.ProcessId);
        process.Kill();
        _owner.UpdateItems();
        return CommandResult.KeepOpen();
    }
}
