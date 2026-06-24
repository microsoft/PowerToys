// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Pure helper methods for sorting and inserting items according to a user-defined
/// extension order. Operates on provider ID strings so the logic is easily testable
/// without constructing heavyweight view-model instances.
/// </summary>
internal static class ExtensionOrderHelper
{
    /// <summary>
    /// Returns a new list with items sorted so that those whose provider is in
    /// <paramref name="extensionOrder"/> appear first (in that order), followed by
    /// the remaining items in their original order.
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

        ordered.Sort((a, b) => orderLookup[providerIdSelector(a)].CompareTo(orderLookup[providerIdSelector(b)]));
        ordered.AddRange(unordered);
        return ordered;
    }

    /// <summary>
    /// Determines where to insert a new provider's items in the existing list based on
    /// <paramref name="extensionOrder"/>. If the provider is in the order list, it's
    /// placed after other ordered providers that precede it. Otherwise it goes at the end.
    /// </summary>
    internal static int FindInsertIndex<T>(List<T> items, string providerId, string[] extensionOrder, Func<T, string> providerIdSelector)
    {
        var providerIndex = Array.IndexOf(extensionOrder, providerId);
        if (providerIndex < 0)
        {
            return items.Count;
        }

        // Find the last item in the list whose provider has a lower order index
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var existingIndex = Array.IndexOf(extensionOrder, providerIdSelector(items[i]));
            if (existingIndex >= 0 && existingIndex < providerIndex)
            {
                // Insert after the last command of this earlier-ordered provider
                var insertAfterProvider = providerIdSelector(items[i]);
                for (var j = i + 1; j < items.Count; j++)
                {
                    if (providerIdSelector(items[j]) != insertAfterProvider)
                    {
                        return j;
                    }
                }

                return i + 1;
            }
        }

        // This provider has the lowest order index among those present — insert at the beginning
        return 0;
    }
}
