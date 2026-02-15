// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

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

        var nameMatchScore = FuzzyStringMatcher.ScoreFuzzy(query, listItem.Title);

        // var locNameMatch = StringMatcher.FuzzySearch(query, NameLocalized);
        var descriptionMatchScore = FuzzyStringMatcher.ScoreFuzzy(query, listItem.Subtitle);

        // var executableNameMatch = StringMatcher.FuzzySearch(query, ExePath);
        // var locExecutableNameMatch = StringMatcher.FuzzySearch(query, ExecutableNameLocalized);
        // var lnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableName);
        // var locLnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableNameLocalized);
        // var score = new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, executableNameMatch.Score }.Max();
        return new[] { nameMatchScore, (descriptionMatchScore - 4) / 2, 0 }.Max();
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
    {
        return FilterListWithScores<T>(items, query, scoreFunction)
                .Select(score => score.Item);
    }

    public static IEnumerable<Scored<T>> FilterListWithScores<T>(IEnumerable<T> items, string query, Func<string, T, int> scoreFunction)
    {
        var scores = items
            .Select(li => new Scored<T>() { Item = li, Score = scoreFunction(query, li) })
            .Where(score => score.Score > 0)
            .OrderByDescending(score => score.Score);
        return scores;
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
        InPlaceUpdateList(original, newContents, out _);
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
    /// <param name="removedItems">List of items that were removed from the original collection</param>
    public static void InPlaceUpdateList<T>(IList<T> original, IEnumerable<T> newContents, out List<T> removedItems)
        where T : class
    {
        removedItems = [];

        // Materialize once: avoids multi-enumeration and inconsistent iterators
        var newList = newContents as IList<T> ?? newContents.ToList();
        var numberOfNew = newList.Count;

        // Short circuit - new contents should just be empty
        if (numberOfNew == 0)
        {
            // Avoid Clear() to reduce UI churn — individual removes let
            // the ListView recycle containers rather than rebuild everything.
            while (original.Count > 0)
            {
                removedItems.Add(original[^1]);
                original.RemoveAt(original.Count - 1);
            }

            return;
        }

        // Build a set of new items for O(1) existence checks.
        // Uses default comparer (same Equals the merge loop uses).
        var newSet = new HashSet<T>(numberOfNew);
        for (var i = 0; i < numberOfNew; i++)
        {
            newSet.Add(newList[i]);
        }

        // Pre-remove items that are not in newList. Iterating backwards keeps
        // earlier indices stable and shrinks the working set for the merge loop.
        for (var i = original.Count - 1; i >= 0; i--)
        {
            if (!newSet.Contains(original[i]))
            {
                removedItems.Add(original[i]);
                original.RemoveAt(i);
            }
        }

        // If we can, use Move to preserve containers/selection better
        var oc = original as ObservableCollection<T>;

        for (var i = 0; i < numberOfNew; i++)
        {
            var newItem = newList[i];

            // If we've run out of original items, append the rest
            if (i >= original.Count)
            {
                original.Add(newItem);
                continue;
            }

            // Already correct?
            if (original[i]?.Equals(newItem) ?? false)
            {
                continue;
            }

            // Find the item later in the original list
            var foundIndex = -1;
            for (var j = i + 1; j < original.Count; j++)
            {
                if (original[j]?.Equals(newItem) ?? false)
                {
                    foundIndex = j;
                    break;
                }
            }

            if (foundIndex >= 0)
            {
                // Bring it to position i WITHOUT deleting intervening items
                if (oc is not null)
                {
                    oc.Move(foundIndex, i);
                }
                else
                {
                    var item = original[foundIndex];
                    original.RemoveAt(foundIndex);
                    original.Insert(i, item);
                }

                continue;
            }

            // Not found: insert new item at i
            original.Insert(i, newItem);
        }

        // Remove any extra trailing items from the destination
        while (original.Count > numberOfNew)
        {
            removedItems.Add(original[^1]);
            original.RemoveAt(original.Count - 1);
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
