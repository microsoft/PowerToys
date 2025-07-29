// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels.Commands;

public sealed partial class ShowDetailsCommand : InvokableCommand
{
    private DetailsViewModel Details { get; set; }

    public ShowDetailsCommand(DetailsViewModel details)
    {
        Id = "ShowDetailsContextAction";
        Name = "Show Details";
        Icon = new IconInfo("\uF000");
        Details = details;
    }

    public override CommandResult Invoke()
    {
        // Send the ShowDetailsMessage when the action is invoked
        WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(Details));
        return CommandResult.KeepOpen();
    }
}
