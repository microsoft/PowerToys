// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class LaunchWorkspaceCommand : InvokableCommand
{
    private readonly string _workspaceId;

    internal LaunchWorkspaceCommand(string workspaceId)
    {
        _workspaceId = workspaceId;
    }

    public override CommandResult Invoke()
    {
        if (string.IsNullOrEmpty(_workspaceId))
        {
            return CommandResult.KeepOpen();
        }

        try
        {
            var launcherPath = PowerToysPathResolver.TryResolveExecutable("PowerToys.WorkspacesLauncher.exe");
            if (string.IsNullOrEmpty(launcherPath))
            {
                return CommandResult.ShowToast("Unable to locate PowerToys Workspaces launcher.");
            }

            var startInfo = new ProcessStartInfo(launcherPath, _workspaceId)
            {
                UseShellExecute = true,
            };

            Process.Start(startInfo);
            return CommandResult.Hide();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Launching workspace failed: {ex.Message}");
        }
    }
}
