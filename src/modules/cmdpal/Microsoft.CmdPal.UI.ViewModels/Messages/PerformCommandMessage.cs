// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Used to do a command - navigate to a page or invoke it
/// </summary>
public record PerformCommandMessage
{
    public ExtensionObject<ICommand> Command { get; }

    public object? Context { get; }

    public bool WithAnimation { get; set; } = true;

    public PerformCommandMessage(ExtensionObject<ICommand> command)
    {
        Command = command;
        Context = null;
    }

    public PerformCommandMessage(TopLevelCommandItemWrapper topLevelCommand)
    {
        Command = new(topLevelCommand.Command);
        Context = null;
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

    public PerformCommandMessage(ConfirmResultViewModel vm)
    {
        Command = vm.PrimaryCommand.Model;
        Context = null;
    }
}
