// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class OpenSettingsCommand : InvokableCommand
{
    public OpenSettingsCommand()
    {
        Name = "Open Settings";
        Icon = new("\uE713");
    }

    public override ICommandResult Invoke()
    {
        WeakReferenceMessenger.Default.Send<OpenSettingsMessage>();
        return CommandResult.KeepOpen();
    }
}
