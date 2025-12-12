// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable IDE0007 // Use implicit type

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

internal static class MainListPageResultFactory
{
    /// <summary>
    /// Creates a merged and ordered array of results from multiple scored input lists,
    /// applying an application result limit and filtering fallback items as needed.
    /// </summary>
    public static IListItem[] Create(
        IList<Scored<IListItem>>? filteredItems,
        IList<Scored<IListItem>>? scoredFallbackItems,
        IList<Scored<IListItem>>? filteredApps,
        IList<Scored<IListItem>>? fallbackItems,
        int appResultLimit)
    {
        if (appResultLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appResultLimit), "App result limit must be non-negative.");
        }

        int len1 = filteredItems?.Count ?? 0;
        int len2 = scoredFallbackItems?.Count ?? 0;

        // Apps are pre-sorted, so we just need to take the top N, limited by appResultLimit.
        int len3 = Math.Min(filteredApps?.Count ?? 0, appResultLimit);

        // Allocate the exact size of the result array.
        int totalCount = len1 + len2 + len3 + GetNonEmptyFallbackItemsCount(fallbackItems);
        var result = new IListItem[totalCount];

        // Three-way stable merge of already-sorted lists.
        int idx1 = 0, idx2 = 0, idx3 = 0;
        int writePos = 0;

        // Merge while all three lists have items. To maintain a stable sort, the
        // priority is: list1 > list2 > list3 when scores are equal.
        while (idx1 < len1 && idx2 < len2 && idx3 < len3)
        {
            // Using null-forgiving operator as we have already checked against lengths.
            int score1 = filteredItems![idx1].Score;
            int score2 = scoredFallbackItems![idx2].Score;
            int score3 = filteredApps![idx3].Score;

            if (score1 >= score2 && score1 >= score3)
            {
                result[writePos++] = filteredItems[idx1++].Item;
            }
            else if (score2 >= score3)
            {
                result[writePos++] = scoredFallbackItems[idx2++].Item;
            }
            else
            {
                result[writePos++] = filteredApps[idx3++].Item;
            }
        }

        // Two-way merges for remaining pairs.
        while (idx1 < len1 && idx2 < len2)
        {
            if (filteredItems![idx1].Score >= scoredFallbackItems![idx2].Score)
            {
                result[writePos++] = filteredItems[idx1++].Item;
            }
            else
            {
                result[writePos++] = scoredFallbackItems[idx2++].Item;
            }
        }

        while (idx1 < len1 && idx3 < len3)
        {
            if (filteredItems![idx1].Score >= filteredApps![idx3].Score)
            {
                result[writePos++] = filteredItems[idx1++].Item;
            }
            else
            {
                result[writePos++] = filteredApps[idx3++].Item;
            }
        }

        while (idx2 < len2 && idx3 < len3)
        {
            if (scoredFallbackItems![idx2].Score >= filteredApps![idx3].Score)
            {
                result[writePos++] = scoredFallbackItems[idx2++].Item;
            }
            else
            {
                result[writePos++] = filteredApps[idx3++].Item;
            }
        }

        // Drain remaining items from a non-empty list.
        while (idx1 < len1)
        {
            result[writePos++] = filteredItems![idx1++].Item;
        }

        while (idx2 < len2)
        {
            result[writePos++] = scoredFallbackItems![idx2++].Item;
        }

        while (idx3 < len3)
        {
            result[writePos++] = filteredApps![idx3++].Item;
        }

        // Append filtered fallback items. Fallback items are added post-sort so they are
        // always at the end of the list and eventually ordered based on user preference.
        if (fallbackItems is not null)
        {
            for (int i = 0; i < fallbackItems.Count; i++)
            {
                var item = fallbackItems[i].Item;
                if (!string.IsNullOrEmpty(item.Title))
                {
                    result[writePos++] = item;
                }
            }
        }

        return result;
    }

    private static int GetNonEmptyFallbackItemsCount(IList<Scored<IListItem>>? fallbackItems)
    {
        int fallbackItemsCount = 0;

        if (fallbackItems is not null)
        {
            for (int i = 0; i < fallbackItems.Count; i++)
            {
                if (!string.IsNullOrEmpty(fallbackItems[i].Item.Title))
                {
                    fallbackItemsCount++;
                }
            }
        }

        return fallbackItemsCount;
    }
}
#pragma warning restore IDE0007 // Use implicit type
