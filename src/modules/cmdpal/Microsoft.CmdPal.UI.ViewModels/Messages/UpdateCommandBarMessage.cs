// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Used to update the command bar at the bottom to reflect the commands for a list item
/// </summary>
public record UpdateCommandBarMessage(ICommandBarContext? ViewModel)
{
}

/// <summary>
/// Provides the command entries and availability used by a context menu.
/// </summary>
public interface IContextMenuContext : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the additional menu entries, excluding the primary command and including
    /// the secondary command when present. The collection may contain separators.
    /// </summary>
    /// <remarks>
    /// The first <see cref="CommandContextItemViewModel"/> is the secondary command.
    /// The collection's entry count includes separators and is not a command count.
    /// </remarks>
    public IReadOnlyList<IContextItemViewModel> MoreCommands { get; }

    /// <summary>
    /// Gets whether <see cref="MoreCommands"/> contains a command entry.
    /// </summary>
    /// <remarks>
    /// A single secondary command is enough. This does not determine More-button visibility
    /// or whether the menu has a visible command; use <see cref="CanOpenContextMenu"/> for menu availability.
    /// </remarks>
    public bool HasMoreCommands { get; }

    /// <summary>
    /// Gets whether the menu has at least one command that should be visible.
    /// </summary>
    /// <remarks>
    /// Includes the primary command when it is represented in the menu. Use this for keyboard
    /// and item context-menu requests, independently of command-bar button visibility.
    /// </remarks>
    public bool CanOpenContextMenu { get; }

    /// <summary>
    /// Gets all entries used to build the menu, including the primary command when present,
    /// additional commands, and separators.
    /// </summary>
    public IReadOnlyList<IContextItemViewModel> AllCommands { get; }

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
        var result = new Dictionary<KeyChord, CommandContextItemViewModel>();

        var menu = MoreCommands;
        if (menu is null)
        {
            return result;
        }

        foreach (var item in menu)
        {
            if (item is CommandContextItemViewModel cmd && cmd.HasRequestedShortcut)
            {
                var key = cmd.RequestedShortcut ?? new KeyChord(0, 0, 0);
                var added = result.TryAdd(key, cmd);
                if (!added)
                {
                    CoreLogger.LogWarning($"Ignoring duplicate keyboard shortcut {KeyChordHelpers.FormatForDebug(key)} on command '{cmd.Title ?? cmd.Name ?? "(unknown)"}'");
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Supplies the primary and secondary actions for the command bar and the associated menu context.
/// </summary>
public interface ICommandBarContext : IContextMenuContext
{
    /// <summary>
    /// Gets the secondary command's name, or an empty string when there is no secondary command.
    /// </summary>
    public string SecondaryCommandName { get; }

    /// <summary>
    /// Gets the command used for the primary action, or <see langword="null"/> when there is none.
    /// </summary>
    public CommandItemViewModel? PrimaryCommand { get; }

    /// <summary>
    /// Gets the first command in <see cref="IContextMenuContext.MoreCommands"/>, skipping separators,
    /// or <see langword="null"/> when there is none. This command also has its own button in the command bar.
    /// </summary>
    public CommandItemViewModel? SecondaryCommand { get; }
}
