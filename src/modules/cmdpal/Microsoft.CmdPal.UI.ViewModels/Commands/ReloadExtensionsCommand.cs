// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class ReloadExtensionsCommand : InvokableCommand
{
    public ReloadExtensionsCommand()
    {
        Icon = new IconInfo("\uE72C"); // Refresh icon
    }

    public override ICommandResult Invoke()
    {
        // 1% BODGY: clear the search before reloading, so that we tell in-proc
        // fallback handlers the empty search text
        WeakReferenceMessenger.Default.Send<ClearSearchMessage>();
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>();
        return CommandResult.GoHome();
    }
}
