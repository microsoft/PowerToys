// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Used to do a command - navigate to a page or invoke it
/// </summary>
public record PerformCommandMessage
{
    public ExtensionObject<ICommand> Command { get; }

    public object? Context { get; }

    public bool WithAnimation { get; set; } = true;

    public ICommandArgument?[] Arguments { get; set; } = [];

    // public PerformCommandMessage(ExtensionObject<ICommand> command)
    // {
    //    Command = command;
    //    Context = null;
    // }
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

    public PerformCommandMessage(CommandItemViewModel item, CommandItemViewModel? context = null)
    {
        Command = item.Command.Model;
        Context = context?.Model.Unsafe ?? item.Model.Unsafe;
        if (item.Parameters != null && item.Parameters.Any())
        {
            Arguments = item.Parameters.Select(p => p.Model.Unsafe).ToArray();
        }
    }

    public PerformCommandMessage(ConfirmResultViewModel vm)
    {
        Command = vm.PrimaryCommand.Model;
        Context = null;
    }
}
