// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public interface IContextMenuFactory
{
    /// <summary>
    /// Builds initialized menu entries in display order, including optional host command slots.
    /// </summary>
    /// <param name="items">Extension-provided context items.</param>
    /// <param name="commandItem">The item owning the menu.</param>
    /// <param name="surface">The item's display surface, or null to omit host command slots.</param>
    /// <returns>The ordered entries; the owning item resolves slots before displaying the menu.</returns>
    /// <remarks>
    /// Include <see cref="ContextMenuSlot.ShowDetails"/> on surfaces supporting a details pane
    /// to offer the optional Show Details action unless an extension supplies the reserved command ID.
    /// Omitting the slot suppresses the host action and avoids allocating its live command.
    /// The factory declares its position; the owning item manages the live command and its visibility.
    /// Snapshot assembly also checks reserved IDs as a fallback for custom factories
    /// that return conflicting slots.
    /// </remarks>
    List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        CommandItemViewModel commandItem,
        ItemSurface? surface);

    void AddMoreCommandsToTopLevel(
        TopLevelViewModel topLevelItem,
        ICommandProviderContext providerContext,
        List<IContextItem?> contextItems);
}
