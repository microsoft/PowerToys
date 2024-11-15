// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListHelpers
{
    // Generate a score for a list item.
    public static int ScoreListItem(string query, IListItem listItem)
    {
        var isFallback = false;
        if (listItem.FallbackHandler != null)
        {
            isFallback = true;

            if (string.IsNullOrWhiteSpace(listItem.Title))
            {
                return 0;
            }
        }

        if (string.IsNullOrEmpty(query))
        {
            return 1;
        }

        var nameMatch = StringMatcher.FuzzySearch(query, listItem.Title);

        // var locNameMatch = StringMatcher.FuzzySearch(query, NameLocalized);
        var descriptionMatch = StringMatcher.FuzzySearch(query, listItem.Subtitle);

        // var executableNameMatch = StringMatcher.FuzzySearch(query, ExePath);
        // var locExecutableNameMatch = StringMatcher.FuzzySearch(query, ExecutableNameLocalized);
        // var lnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableName);
        // var locLnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableNameLocalized);
        // var score = new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, executableNameMatch.Score }.Max();
        return new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, 0 }.Max() / (isFallback ? 3 : 1);
    }

    public static IEnumerable<IListItem> FilterList(IEnumerable<IListItem> items, string query)
    {
        var scores = items
            .Select(li => new ScoredListItem() { ListItem = li, Score = ScoreListItem(query, li) })
            .Where(score => score.Score > 0)
            .OrderByDescending(score => score.Score);
        return scores
            .Select(score => score.ListItem);
    }

    public static void InPlaceUpdateList<T>(Collection<T> original, Collection<T> newContents)
        where T : class
    {
        for (var i = 0; i < original.Count && i < newContents.Count; i++)
        {
            for (var j = i; j < original.Count; j++)
            {
                var og_2 = original[j];
                var newItem_2 = newContents[i];
                var areEqual_2 = og_2.Equals(newItem_2);
                if (areEqual_2)
                {
                    for (var k = i; k < j; k++)
                    {
                        // This item from the original list was not in the new list. Remove it.
                        original.RemoveAt(i);
                    }

                    break;
                }
            }

            var og = original[i];
            var newItem = newContents[i];
            var areEqual = og.Equals(newItem);

            // Is this new item already in the list?
            if (areEqual)
            {
                // It is already in the list
            }
            else
            {
                // it isn't. Add it.
                original.Insert(i, newContents[i]);
            }
        }

        // Remove any extra trailing items from the destination
        while (original.Count > newContents.Count)
        {
            // RemoveAtEnd
            original.RemoveAt(original.Count - 1);
        }

        // Add any extra trailing items from the source
        while (original.Count < newContents.Count)
        {
            original.Add(newContents[original.Count]);
        }
    }
}

public struct ScoredListItem
{
    public int Score;
    public IListItem ListItem;
}
