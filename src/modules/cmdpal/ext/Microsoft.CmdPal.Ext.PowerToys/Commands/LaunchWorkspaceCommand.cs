// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Commands;

// Responsible for launch a specific workspace
internal sealed partial class LaunchWorkspaceCommand : InvokableCommand
{
    private readonly string _workspaceId;

    public LaunchWorkspaceCommand(string workspaceId)
    {
        _workspaceId = workspaceId;
    }

    public override CommandResult Invoke()
    {
        LaunchWorkspace();
        return CommandResult.KeepOpen();
    }

    private bool LaunchWorkspace()
    {
        var powertoysBaseDir = PowerToysPathResolver.GetPowerToysInstallPath();
        if (string.IsNullOrEmpty(powertoysBaseDir))
        {
            return false;
        }

        var launcherPath = Path.Combine(powertoysBaseDir, "PowerToys.WorkspacesLauncher.exe");

        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = launcherPath;
        process.StartInfo.Arguments = _workspaceId;
        process.StartInfo.UseShellExecute = true;

        // process.StartInfo.Verb = "runas"; // run as admin
        process.Start();

        return true;
    }
}
