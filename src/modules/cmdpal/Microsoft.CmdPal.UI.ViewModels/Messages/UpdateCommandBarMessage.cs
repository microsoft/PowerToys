// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Updates the command bar for a selected item or content page.
/// </summary>
public record UpdateCommandBarMessage(ICommandBarContext? ViewModel)
{
}

/// <summary>
/// Provides the complete, ordered menu and its availability.
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
    /// Maps requested shortcuts from the full menu, including the primary action when it requests one.
    /// </summary>
    /// <returns>The first command for each requested shortcut, ignoring separators.</returns>
    public Dictionary<KeyChord, CommandContextItemViewModel> Keybindings() => CreateKeybindings(AllCommands);

    // Root and nested menus share the same shortcut and duplicate-resolution policy.
    internal static Dictionary<KeyChord, CommandContextItemViewModel> CreateKeybindings(IEnumerable<IContextItemViewModel>? menu)
    {
        var result = new Dictionary<KeyChord, CommandContextItemViewModel>();

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
/// Adds explicit command-bar actions and overflow availability to the menu context.
/// </summary>
public interface ICommandBarContext : IContextMenuContext
{
    /// <summary>
    /// Gets the secondary action's name, or an empty string when there is no secondary action.
    /// </summary>
    public string SecondaryCommandName { get; }

    /// <summary>
    /// Gets the primary action with its original invocation context, or <see langword="null"/>.
    /// A menu may represent this action with a separate synthetic entry.
    /// </summary>
    public CommandItemViewModel? PrimaryCommand { get; }

    /// <summary>
    /// Gets the visible secondary action shown in the command bar, or <see langword="null"/>.
    /// Separators and hidden commands are skipped when selecting this action.
    /// </summary>
    public CommandItemViewModel? SecondaryCommand { get; }

    /// <summary>
    /// Gets whether the menu contains visible command entries beyond the primary and secondary actions.
    /// </summary>
    /// <remarks>
    /// Separators and hidden commands do not count. This controls the More button's visibility; use
    /// <see cref="IContextMenuContext.CanOpenContextMenu"/> for menu availability.
    /// </remarks>
    public bool HasOverflowCommands { get; }
}
