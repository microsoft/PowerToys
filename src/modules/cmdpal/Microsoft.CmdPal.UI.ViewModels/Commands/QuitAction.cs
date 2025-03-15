// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class QuitCommand : InvokableCommand, IFallbackHandler
{
    public QuitCommand()
    {
        Icon = new IconInfo("\uE711");
    }

    public override ICommandResult Invoke()
    {
        WeakReferenceMessenger.Default.Send<QuitMessage>();
        return CommandResult.KeepOpen();
    }

    // this sneaky hidden behavior, I'm not event gonna try to localize this.
    public void UpdateQuery(string query) => Name = query.StartsWith('q') ? "Quit" : string.Empty;
}
