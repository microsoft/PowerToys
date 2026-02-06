// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Used to do a command - navigate to a page or invoke it
/// </summary>
public record PerformCommandMessage
{
    public ExtensionObject<ICommand> Command { get; }

    public object? Context { get; }

    public bool WithAnimation { get; set; } = true;

    public bool TransientPage { get; set; }

    public bool OpenAsFlyout { get; private set; }

    public Point? FlyoutPosition { get; private set; }

    public PerformCommandMessage(ExtensionObject<ICommand> command)
    {
        Command = command;
        Context = null;
    }

    public static PerformCommandMessage CreateFlyoutMessage(ExtensionObject<ICommand> command, Point flyoutPosition)
    {
        var message = new PerformCommandMessage(command)
        {
            WithAnimation = false,
            TransientPage = true,
            OpenAsFlyout = true,
            FlyoutPosition = flyoutPosition,
        };
        return message;
    }

    public PerformCommandMessage(ExtensionObject<ICommand> command, ExtensionObject<IListItem> context)
    {
        Command = command;
        Context = context.Unsafe;
    }

    public PerformCommandMessage(ExtensionObject<ICommand> command, ExtensionObject<ICommandItem> context)
    {
        Command = command;
        Context = context.Unsafe;
    }

    public PerformCommandMessage(ExtensionObject<ICommand> command, ExtensionObject<ICommandContextItem> context)
    {
        Command = command;
        Context = context.Unsafe;
    }

    public PerformCommandMessage(CommandContextItemViewModel contextCommand)
    {
        Command = contextCommand.Command.Model;
        Context = contextCommand.Model.Unsafe;
    }

    public PerformCommandMessage(ConfirmResultViewModel vm)
    {
        Command = vm.PrimaryCommand.Model;
        Context = null;
    }
}
