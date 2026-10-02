// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Describes the UI capabilities of the surface displaying an item.
/// </summary>
/// <remarks>
/// Dialog hosting expands the shell when needed; surfaces do not hide commands
/// because any extension command may return <c>CommandResult.Confirm</c>.
/// </remarks>
public sealed class ItemSurface
{
    public static readonly ItemSurface CommandPalette = new(nameof(CommandPalette), supportsDetailsPane: true);
    public static readonly ItemSurface QuickAccessShelf = new(nameof(QuickAccessShelf), supportsDetailsPane: false);

    public string Name { get; }

    public bool SupportsDetailsPane { get; }

    private ItemSurface(string name, bool supportsDetailsPane)
    {
        Name = name;
        SupportsDetailsPane = supportsDetailsPane;
    }

    public override string ToString() => Name;
}
