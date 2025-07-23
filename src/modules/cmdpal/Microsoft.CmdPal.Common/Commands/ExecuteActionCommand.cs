// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions.Hosting;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

public sealed partial class ExecuteActionCommand : InvokableCommand
{
    private readonly ActionInstance actionInstance;

    public ExecuteActionCommand(ActionInstance actionInstance)
    {
        this.actionInstance = actionInstance;
        this.Name = actionInstance.DisplayInfo.Description;
        this.Icon = new IconInfo(actionInstance.Definition.IconFullPath);
    }

    public override CommandResult Invoke()
    {
        var task = Task.Run(InvokeAsync);
        task.Wait();

        return task.Result;
    }

    private async Task<CommandResult> InvokeAsync()
    {
        try
        {
            await actionInstance.InvokeAsync();
            return CommandResult.GoHome();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast("Failed to invoke action " + actionInstance.Definition.Id + ": " + ex.Message);
        }
    }
}
