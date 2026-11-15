// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

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
