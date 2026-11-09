// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Describes the context and presentation of a menu to open.
/// </summary>
/// <param name="Context">The item or page whose cached commands populate the menu.</param>
public record ContextMenuRequest(IContextMenuContext Context)
{
    /// <summary>
    /// Gets the complete anchor override, or null to use the host's current default.
    /// </summary>
    public ContextMenuAnchor? Anchor { get; init; }

    /// <summary>
    /// Gets the root command whose submenu should open initially, or null to open the root menu.
    /// </summary>
    public CommandContextItemViewModel? InitialSubmenu { get; init; }

    /// <summary>
    /// Gets whether the filter box is shown. Defaults to true.
    /// </summary>
    public bool ShowFilterBox { get; init; } = true;
}
