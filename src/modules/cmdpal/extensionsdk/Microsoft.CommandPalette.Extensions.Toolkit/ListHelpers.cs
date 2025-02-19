// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ListHelpers
{
    // Generate a score for a list item.
    public static int ScoreListItem(string query, ICommandItem listItem)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        if (string.IsNullOrEmpty(listItem.Title))
        {
            return 0;
        }

        var nameMatch = StringMatcher.FuzzySearch(query, listItem.Title);

        // var locNameMatch = StringMatcher.FuzzySearch(query, NameLocalized);
        var descriptionMatch = StringMatcher.FuzzySearch(query, listItem.Subtitle);

        // var executableNameMatch = StringMatcher.FuzzySearch(query, ExePath);
        // var locExecutableNameMatch = StringMatcher.FuzzySearch(query, ExecutableNameLocalized);
        // var lnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableName);
        // var locLnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableNameLocalized);
        // var score = new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, executableNameMatch.Score }.Max();
        return new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, 0 }.Max();
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

    public static IEnumerable<T> FilterList<T>(IEnumerable<T> items, string query, Func<string, T, int> scoreFunction)
        where T : class
    {
        var scores = items
            .Select(li => new Scored<T>() { Item = li, Score = scoreFunction(query, li) })
            .Where(score => score.Score > 0)
            .OrderByDescending(score => score.Score);
        return scores
            .Select(score => score.Item);
    }

    /// <summary>
    /// Modifies the contents of `original` in-place, to match those of
    /// `newContents`. The canonical use being:
    /// ```cs
    /// ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(ItemsToFilter, TextToFilterOn));
    /// ```
    /// </summary>
    /// <typeparam name="T">Any type that can be compared for equality</typeparam>
    /// <param name="original">Collection to modify</param>
    /// <param name="newContents">The enumerable which `original` should match</param>
    public static void InPlaceUpdateList<T>(IList<T> original, IEnumerable<T> newContents)
        where T : class
    {
        // we're not changing newContents - stash this so we don't re-evaluate it every time
        var numberOfNew = newContents.Count();

        // Short circuit - new contents should just be empty
        if (numberOfNew == 0)
        {
            original.Clear();
            return;
        }

        var i = 0;
        foreach (var newItem in newContents)
        {
            if (i >= original.Count)
            {
                break;
            }

            for (var j = i; j < original.Count; j++)
            {
                var og_2 = original[j];
                var areEqual_2 = og_2?.Equals(newItem) ?? false;
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
            var areEqual = og?.Equals(newItem) ?? false;

            // Is this new item already in the list?
            if (areEqual)
            {
                // It is already in the list
            }
            else
            {
                // it isn't. Add it.
                original.Insert(i, newItem);
            }

            i++;
        }

        // Remove any extra trailing items from the destination
        while (original.Count > numberOfNew)
        {
            // RemoveAtEnd
            original.RemoveAt(original.Count - 1);
        }

        // Add any extra trailing items from the source
        if (original.Count < numberOfNew)
        {
            var remaining = newContents.Skip(original.Count);
            foreach (var item in remaining)
            {
                original.Add(item);
            }
        }
    }
}

public struct ScoredListItem
{
    public int Score;
    public IListItem ListItem;
}

public struct Scored<T>
{
    public int Score;
    public T Item;
}
