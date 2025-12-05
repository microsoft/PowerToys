// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Workspaces.ModuleServices;

namespace PowerToysExtension.Commands;

internal sealed partial class OpenWorkspaceEditorCommand : InvokableCommand
{
    public override CommandResult Invoke()
    {
        var result = WorkspaceService.Instance.LaunchEditorAsync().GetAwaiter().GetResult();
        if (!result.Success)
        {
            return CommandResult.ShowToast(result.Error ?? "Unable to launch the Workspaces editor.");
        }

        return CommandResult.Hide();
    }
}
