// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Pure helper methods for sorting items according to a user-defined extension
/// order. Operates on provider ID strings so the logic is easily testable without
/// constructing heavyweight view-model instances.
/// </summary>
internal static class ExtensionOrderHelper
{
    /// <summary>
    /// Returns a new list with items sorted so that those whose provider is in
    /// <paramref name="extensionOrder"/> appear first (in that order), followed by
    /// the remaining items in their original order. The sort is stable: items that
    /// share a provider (and therefore the same order index) keep their original
    /// relative order, so a single provider's commands are never shuffled.
    /// </summary>
    internal static List<T> SortByExtensionOrder<T>(List<T> items, string[] extensionOrder, Func<T, string> providerIdSelector)
    {
        var orderLookup = new Dictionary<string, int>(extensionOrder.Length, StringComparer.Ordinal);
        for (var i = 0; i < extensionOrder.Length; i++)
        {
            orderLookup.TryAdd(extensionOrder[i], i);
        }

        var ordered = new List<T>(items.Count);
        var unordered = new List<T>(items.Count);

        foreach (var item in items)
        {
            if (orderLookup.ContainsKey(providerIdSelector(item)))
            {
                ordered.Add(item);
            }
            else
            {
                unordered.Add(item);
            }
        }

        // OrderBy is a stable sort, so commands from the same provider keep the
        // relative order in which they were loaded.
        var result = ordered.OrderBy(item => orderLookup[providerIdSelector(item)]).ToList();
        result.AddRange(unordered);
        return result;
    }
}
