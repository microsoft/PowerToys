// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class OpenSettingsCommand : InvokableCommand
{
    public OpenSettingsCommand()
    {
        Name = Properties.Resources.builtin_open_settings_name;
        Icon = new IconInfo("\uE713");
    }

    public override ICommandResult Invoke()
    {
        WeakReferenceMessenger.Default.Send<OpenSettingsMessage>();
        return CommandResult.KeepOpen();
    }
}
