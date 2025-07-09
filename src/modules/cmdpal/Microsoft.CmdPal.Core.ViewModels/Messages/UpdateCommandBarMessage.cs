// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Used to update the command bar at the bottom to reflect the commands for a list item
/// </summary>
public record UpdateCommandBarMessage(ICommandBarContext? ViewModel)
{
}

public interface IContextMenuContext : INotifyPropertyChanged
{
    public IEnumerable<CommandContextItemViewModel> MoreCommands { get; }

    public bool HasMoreCommands { get; }

    public List<CommandContextItemViewModel> AllCommands { get; }

    /// <summary>
    /// Generates a mapping of key -> command item for this particular item's
    /// MoreCommands. (This won't include the primary Command, but it will
    /// include the secondary one). This map can be used to quickly check if a
    /// shortcut key was pressed
    /// </summary>
    /// <returns>a dictionary of KeyChord -> Context commands, for all commands
    /// that have a shortcut key set.</returns>
    public Dictionary<KeyChord, CommandContextItemViewModel> Keybindings()
    {
        return MoreCommands
            .Where(c => c.HasRequestedShortcut)
            .ToDictionary(
            c => c.RequestedShortcut ?? new KeyChord(0, 0, 0),
            c => c);
    }
}

// Represents everything the command bar needs to know about to show command
// buttons at the bottom.
//
// This is implemented by both ListItemViewModel and ContentPageViewModel,
// the two things with sub-commands.
public interface ICommandBarContext : IContextMenuContext
{
    public string SecondaryCommandName { get; }

    public CommandItemViewModel? PrimaryCommand { get; }

    public CommandItemViewModel? SecondaryCommand { get; }
}
