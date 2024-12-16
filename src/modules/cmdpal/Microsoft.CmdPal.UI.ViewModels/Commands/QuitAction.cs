// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class QuitAction : InvokableCommand, IFallbackHandler
{
    public QuitAction()
    {
        Icon = new("\uE711");

        // TODO HACK: Just always make this command visible, because fallback commands aren't hooked up yet
        Name = "Quit";
    }

    public override ICommandResult Invoke()
    {
        WeakReferenceMessenger.Default.Send<QuitMessage>();
        return CommandResult.KeepOpen();
    }

    public void UpdateQuery(string query) => Name = query.StartsWith('q') ? "Quit" : string.Empty;
}
