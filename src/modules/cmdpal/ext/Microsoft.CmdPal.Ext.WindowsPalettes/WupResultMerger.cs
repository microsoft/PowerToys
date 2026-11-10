// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal static class WupResultMerger
{
    // Items with a Score come first, highest score first. Items without a Score keep their source order
    // after the scored items. Items that share a primary kind and KindSpecificId are collapsed, keeping
    // the highest-ranked one.
    internal static IListItem[] Merge(IReadOnlyList<WupSourcePage> sources)
    {
        List<(WupListItemAdapter Item, int Order)> entries = [];
        var order = 0;
        foreach (var source in sources)
        {
            foreach (var adapter in source.GetAdapters())
            {
                entries.Add((adapter, order++));
            }
        }

        entries.Sort(static (left, right) =>
        {
            var comparison = CompareScores(left.Item.Score, right.Item.Score);
            return comparison != 0 ? comparison : left.Order.CompareTo(right.Order);
        });

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        var merged = new List<IListItem>(entries.Count);
        foreach (var (item, _) in entries)
        {
            var key = item.DeduplicationKey;
            if (key is not null && !seen.Add(key))
            {
                continue;
            }

            merged.Add(item);
        }

        return [.. merged];
    }

    private static int CompareScores(double? left, double? right) => (left, right) switch
    {
        (null, null) => 0,
        (null, _) => 1,
        (_, null) => -1,
        _ => right.Value.CompareTo(left.Value),
    };
}
