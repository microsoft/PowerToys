// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.CmdPal;

internal sealed partial class OpenTryRunCommand : InvokableCommand
{
    private readonly string application;
    private readonly string[] selection;

    public OpenTryRunCommand(string application, string[] selection)
    {
        this.application = application;
        this.selection = selection.ToArray();
        Id = selection.Length == 0 ? "com.microsoft.powertoys.tryrun.open" : "com.microsoft.powertoys.tryrun.selection";
        Name = "Configure in Try Run";
        Icon = new IconInfo("\uE768");
    }

    public override ICommandResult Invoke()
    {
        try
        {
            CmdPalLaunch.OpenAsync(application, selection, CancellationToken.None).GetAwaiter().GetResult();
            return CommandResult.Dismiss();
        }
        catch (Exception exception)
        {
            return CommandResult.ShowToast(new ToastArgs { Message = "Try Run could not open. " + exception.Message, Result = CommandResult.KeepOpen() });
        }
    }
}
