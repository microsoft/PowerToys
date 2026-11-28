// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>
/// The item a page shows when it lists nothing, made again only when what it says changes. A page gives it from its
/// <c>EmptyContent</c> getter, worked out when Command Palette asks as it opens the page: Command Palette puts the item's
/// command, such as Open Text Expander settings, in its command bar only if it already has the item when the list comes in.
/// Set while Command Palette fetched the list, the item came too late, and its command didn't show.
/// </summary>
internal sealed class EmptyStateItem
{
    private readonly Lock _lock = new();
    private EmptyState? _state;
    private ICommandItem? _item;

    /// <summary>Gets the item for <paramref name="state"/>, and whether it's a new one: then the page says it changed.</summary>
    public ICommandItem For(EmptyState state, out bool changed)
    {
        lock (_lock)
        {
            changed = _item is null || _state != state;
            if (changed)
            {
                _state = state;
                _item = state.ToCommandItem();
            }

            return _item!;
        }
    }
}
