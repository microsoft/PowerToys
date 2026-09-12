// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Keeps one ordered menu and its derived action roles together for atomic publication.
/// </summary>
internal sealed class CommandContextSnapshot(
    IContextItemViewModel[] allCommands,
    CommandItemViewModel? primaryCommand,
    CommandContextItemViewModel? secondaryCommand,
    bool hasOverflowCommands,
    bool hasSubmenu)
{
    public static CommandContextSnapshot Empty { get; } = new([], null, null, false, hasSubmenu: false);

    public IContextItemViewModel[] AllCommands { get; } = allCommands;

    public CommandItemViewModel? PrimaryCommand { get; } = primaryCommand;

    public CommandContextItemViewModel? SecondaryCommand { get; } = secondaryCommand;

    public bool HasOverflowCommands { get; } = hasOverflowCommands;

    /// <summary>
    /// Gets whether the owning command has child actions, independent of their visibility.
    /// </summary>
    public bool HasSubmenu { get; } = hasSubmenu;
}
