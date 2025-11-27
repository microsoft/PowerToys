// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.CmdPal.Ext.PowerToys.Services;

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
        var result = ModuleServices.Workspaces().LaunchWorkspaceAsync(_workspaceId).GetAwaiter().GetResult();
        if (!result.Success)
        {
            return CommandResult.ShowToast(result.Error ?? "Failed to launch workspace.");
        }

        return CommandResult.KeepOpen();
    }
}
