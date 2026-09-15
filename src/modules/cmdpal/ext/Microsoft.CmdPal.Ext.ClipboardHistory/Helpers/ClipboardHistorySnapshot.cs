// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed class ClipboardHistorySnapshot
{
    private readonly Dictionary<string, IListItem> _itemsById;

    public static ClipboardHistorySnapshot Empty { get; } = new([], []);

    public IListItem[] Items { get; }

    private ClipboardHistorySnapshot(Dictionary<string, IListItem> itemsById, IListItem[] items)
    {
        _itemsById = itemsById;
        Items = items;
    }

    public async Task<ClipboardHistorySnapshot> RefreshAsync(IReadOnlyList<ClipboardHistoryEntry> entries, CancellationToken cancellationToken)
    {
        Dictionary<string, IListItem> itemsById = new(entries.Count, StringComparer.Ordinal);
        var items = new IListItem[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            if (!_itemsById.TryGetValue(entry.Id, out var item))
            {
                item = await entry.LoadAsync(cancellationToken);
            }

            itemsById.Add(entry.Id, item);
            items[i] = item;
        }

        return new ClipboardHistorySnapshot(itemsById, items);
    }
}
