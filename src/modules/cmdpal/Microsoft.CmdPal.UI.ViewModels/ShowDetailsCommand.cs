// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

public sealed partial class ShowDetailsCommand : InvokableCommand
{
    public static string ShowDetailsCommandId { get; } = "com.microsoft.cmdpal.showDetails";

    private static IconInfo ShowIcon { get; } = new IconInfo("\uF000"); // KnowledgeArticle Icon

    private static IconInfo HideIcon { get; } = new IconInfo("\uED1A"); // Hide Icon

    private DetailsViewModel Details { get; set; }

    private bool _isDetailsVisible;

    public ShowDetailsCommand(DetailsViewModel details)
    {
        Id = ShowDetailsCommandId;
        Name = UI.ViewModels.Properties.Resources.ShowDetailsCommand;
        Icon = ShowIcon;
        Details = details;
    }

    public override CommandResult Invoke()
    {
        _isDetailsVisible = !_isDetailsVisible;

        if (_isDetailsVisible)
        {
            WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(Details));
            Name = UI.ViewModels.Properties.Resources.HideDetailsCommand;
            Icon = HideIcon;
        }
        else
        {
            WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
            Name = UI.ViewModels.Properties.Resources.ShowDetailsCommand;
            Icon = ShowIcon;
        }

        return CommandResult.KeepOpen();
    }
}
