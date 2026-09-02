// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Describes the UI capabilities available where a context menu is displayed.
/// Dialog hosting is handled at the dialog site by expanding the shell; placements do not hide
/// commands because any extension command may return <c>CommandResult.Confirm</c>.
/// </summary>
public sealed class ContextMenuPlacement
{
    public static readonly ContextMenuPlacement CommandPalette = new(nameof(CommandPalette), supportsDetailsPane: true);
    public static readonly ContextMenuPlacement QuickAccessShelf = new(nameof(QuickAccessShelf), supportsDetailsPane: false);
    public static readonly ContextMenuPlacement Dock = new(nameof(Dock), supportsDetailsPane: false);

    public string Name { get; }

    public bool SupportsDetailsPane { get; }

    private ContextMenuPlacement(string name, bool supportsDetailsPane)
    {
        Name = name;
        SupportsDetailsPane = supportsDetailsPane;
    }

    public override string ToString() => Name;
}
