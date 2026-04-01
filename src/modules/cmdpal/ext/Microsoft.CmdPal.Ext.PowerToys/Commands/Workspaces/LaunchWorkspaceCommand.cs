// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Workspaces.ModuleServices;

namespace PowerToysExtension.Commands;

internal sealed partial class LaunchWorkspaceCommand : InvokableCommand
{
    private readonly string _workspaceId;

    internal LaunchWorkspaceCommand(string workspaceId)
    {
        _workspaceId = workspaceId;
        Name = "Launch workspace";
    }

    public override CommandResult Invoke()
    {
        if (string.IsNullOrEmpty(_workspaceId))
        {
            return CommandResult.KeepOpen();
        }

        var result = WorkspaceService.Instance.LaunchWorkspaceAsync(_workspaceId).GetAwaiter().GetResult();
        if (!result.Success)
        {
            return CommandResult.ShowToast(result.Error ?? "Launching workspace failed.");
        }

        return CommandResult.Hide();
    }
}
