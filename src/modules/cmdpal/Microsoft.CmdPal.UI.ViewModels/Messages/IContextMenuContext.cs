// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Provides the complete, ordered menu and its availability.
/// Members read cached host state; extension properties are fetched during background initialization.
/// SDK command collections are normalized before reaching this presentation contract.
/// </summary>
public interface IContextMenuContext : INotifyPropertyChanged
{
    /// <summary>
    /// Gets all menu entries, including the primary action when represented in the menu,
    /// secondary and other actions, and separators. Entry count includes separators.
    /// </summary>
    public IReadOnlyList<IContextItemViewModel> AllCommands { get; }

    /// <summary>
    /// Gets whether the menu has at least one command that should be visible.
    /// </summary>
    /// <remarks>
    /// Used for keyboard and item context menu requests, independently of button visibility
    /// in the command bar.
    /// </remarks>
    public bool CanOpenContextMenu { get; }

    /// <summary>
    /// Finds the first command requesting a non-reserved shortcut, including the primary menu entry.
    /// </summary>
    public CommandContextItemViewModel? FindKeybinding(KeyChord chord) => FindKeybinding(AllCommands, chord);

    // Root and nested menus share the same shortcut and duplicate-resolution policy.
    internal static CommandContextItemViewModel? FindKeybinding(IReadOnlyList<IContextItemViewModel>? menu, KeyChord chord)
    {
        if (menu is null)
        {
            return null;
        }

        for (var i = 0; i < menu.Count; i++)
        {
            if (menu[i] is CommandContextItemViewModel command && command.HasRequestedShortcut && command.RequestedShortcut == chord)
            {
                return command;
            }
        }

        return null;
    }
}
