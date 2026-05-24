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
    /// Modifies the contents of <paramref name="original"/> in-place, to match those of
    /// <paramref name="newContents"/>.
    /// <example>
    /// The canonical use being:
    /// <code>
    /// ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(ItemsToFilter, TextToFilterOn));
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="T">Any type that can be compared for equality</typeparam>
    /// <param name="original">Collection to modify</param>
    /// <param name="newContents">The enumerable which <c>original</c> should match</param>
    public static void InPlaceUpdateList<T>(IList<T> original, IEnumerable<T> newContents)
        where T : class
    {
        // Materialize once to avoid re-enumeration
        var newList = newContents as IList<T> ?? newContents.ToList();
        var numberOfNew = newList.Count;

        // Detect if we can use Move() for better ObservableCollection performance
        var observableCollection = original as ObservableCollection<T>;

        // Short circuit - new contents should just be empty
        if (numberOfNew == 0)
        {
            if (observableCollection is not null)
            {
                // Clear() is observable collection causes a reset notification, which causes
                // the ListView to discard all containers and recreate them, which is expensive and
                // causes ListView to flash.
                while (observableCollection.Count > 0)
                {
                    observableCollection.RemoveAt(observableCollection.Count - 1);
                }
            }
            else
            {
                original.Clear();
            }

            return;
        }

        // Simple forward-scan merge. No HashSet needed because we don't track
        // removed items — the icon-bug guard is unnecessary, and items removed
        // mid-merge that appear later in newList will simply be re-inserted.
        var i = 0;
        for (var newIndex = 0; newIndex < numberOfNew; newIndex++)
        {
            var newItem = newList[newIndex];

            if (i >= original.Count)
            {
                break;
            }

            // Search for this item in the remaining portion of original
            var foundIndex = -1;
            for (var j = i; j < original.Count; j++)
            {
                if (original[j]?.Equals(newItem) ?? false)
                {
                    foundIndex = j;
                    break;
                }
            }

            if (foundIndex >= 0)
            {
                // Remove all items between i and foundIndex
                for (var k = foundIndex - 1; k >= i; k--)
                {
                    original.RemoveAt(k);
                    foundIndex--;
                }

                // If the found item isn't at position i yet, move it there
                if (foundIndex > i)
                {
                    MoveItem(original, observableCollection, foundIndex, i);
                }
            }
            else
            {
                // Not found - insert new item at position i
                original.Insert(i, newItem);
            }

            i++;
        }

        // Remove any extra trailing items from the destination
        while (original.Count > numberOfNew)
        {
            original.RemoveAt(original.Count - 1);
        }

        // Add any extra trailing items from the source
        while (i < numberOfNew)
        {
            original.Add(newList[i]);
            i++;
        }
    }

    /// <summary>
    /// Modifies the contents of <paramref name="original"/> in-place, to match those of
    /// <paramref name="newContents"/>.
    /// <example>
    /// The canonical use being:
    /// <code>
    /// ListHelpers.InPlaceUpdateList(FilteredItems, FilterList(ItemsToFilter, TextToFilterOn), out var removedItems);
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="T">Any type that can be compared for equality</typeparam>
    /// <param name="original">Collection to modify</param>
    /// <param name="newContents">The enumerable which <c>original</c> should match</param>
    /// <param name="removedItems">List of items that were removed from the original collection</param>
    public static void InPlaceUpdateList<T>(IList<T> original, IEnumerable<T> newContents, out List<T> removedItems)
        where T : class
    {
        removedItems = [];

        // Materialize once to avoid re-enumeration
        var newList = newContents as IList<T> ?? newContents.ToList();
        var numberOfNew = newList.Count;

        // Short circuit - new contents should just be empty
        if (numberOfNew == 0)
        {
            while (original.Count > 0)
            {
                removedItems.Add(original[^1]);
                original.RemoveAt(original.Count - 1);
            }

            return;
        }

        // Detect if we can use Move() for better ObservableCollection performance
        var observableCollection = original as ObservableCollection<T>;

        // Build a set of new items for O(1) existence checks.
        var newSet = new HashSet<T>(numberOfNew);
        for (var i = 0; i < numberOfNew; i++)
        {
            newSet.Add(newList[i]);
        }

        // When there is zero overlap (e.g. navigating between pages), use
        // indexed replacement instead of Insert + Remove. Each Replace reuses
        // the container slot and fires one notification instead of two.
        var hasOverlap = false;
        for (var i = 0; i < original.Count; i++)
        {
            if (newSet.Contains(original[i]))
            {
                hasOverlap = true;
                break;
            }
        }

        if (!hasOverlap)
        {
            var minLen = Math.Min(original.Count, numberOfNew);
            for (var i = 0; i < minLen; i++)
            {
                removedItems.Add(original[i]);
                original[i] = newList[i]; // Replace — single notification, container reused
            }

            while (original.Count > numberOfNew)
            {
                removedItems.Add(original[^1]);
                original.RemoveAt(original.Count - 1);
            }

            for (var i = minLen; i < numberOfNew; i++)
            {
                original.Add(newList[i]);
            }

            return;
        }

        // Large collections benefit from pre-filtering (O(n) removal shrinks the
        // working set), which outweighs the extra pass. Small collections are
        // faster with lazy removal during the merge. Threshold determined empirically.
        if (original.Count >= 5000)
        {
            MergeWithPreRemoval(original, newList, numberOfNew, newSet, observableCollection, removedItems);
        }
        else
        {
            MergeWithLazyRemoval(original, newList, numberOfNew, newSet, observableCollection, removedItems);
        }
    }

    /// <summary>
    /// Fast path for small/medium collections. Removes non-matching items lazily
    /// during the forward-scan merge, avoiding a separate pre-removal pass.
    /// </summary>
    private static void MergeWithLazyRemoval<T>(
        IList<T> original,
        IList<T> newList,
        int numberOfNew,
        HashSet<T> newSet,
        ObservableCollection<T>? observableCollection,
        List<T>? removedItems)
    where T : class
    {
        var i = 0;
        for (var newIndex = 0; newIndex < numberOfNew; newIndex++)
        {
            var newItem = newList[newIndex];

            if (i >= original.Count)
            {
                break;
            }

            // Search for this item in the remaining portion of original
            var foundIndex = -1;
            for (var j = i; j < original.Count; j++)
            {
                if (original[j]?.Equals(newItem) ?? false)
                {
                    foundIndex = j;
                    break;
                }
            }

            if (foundIndex >= 0)
            {
                // Remove only items between i and foundIndex that are NOT in newList.
                // Items still needed later stay in the collection, avoiding
                // unnecessary Remove+Insert cycles and extra UI notifications.
                for (var k = foundIndex - 1; k >= i; k--)
                {
                    if (!newSet.Contains(original[k]))
                    {
                        removedItems?.Add(original[k]);
                        original.RemoveAt(k);
                        foundIndex--;
                    }
                }

                // If the found item isn't at position i yet, move it there
                if (foundIndex > i)
                {
                    MoveItem(original, observableCollection, foundIndex, i);
                }
            }
            else
            {
                // Not found - insert new item at position i
                original.Insert(i, newItem);
            }

            i++;
        }

        // Remove any extra trailing items from the destination
        while (original.Count > numberOfNew)
        {
            removedItems?.Add(original[^1]);
            original.RemoveAt(original.Count - 1);
        }

        // Add any extra trailing items from the source
        while (i < numberOfNew)
        {
            original.Add(newList[i]);
            i++;
        }
    }

    /// <summary>
    /// Path for large collections. Pre-removes non-matching items to shrink the
    /// working set before merging, making linear searches faster.
    /// </summary>
    private static void MergeWithPreRemoval<T>(
        IList<T> original,
        IList<T> newList,
        int numberOfNew,
        HashSet<T> newSet,
        ObservableCollection<T>? observableCollection,
        List<T>? removedItems)
    where T : class
    {
        // Pre-remove items that are not in newList. Iterating backwards keeps
        // earlier indices stable and shrinks the working set for the merge loop.
        for (var i = original.Count - 1; i >= 0; i--)
        {
            if (!newSet.Contains(original[i]))
            {
                removedItems?.Add(original[i]);
                original.RemoveAt(i);
            }
        }

        // Forward-scan merge: move or insert items to match newList order.
        // After pre-removal, original only contains items that exist in newList,
        // so the merge loop is simple — just Move or Insert.
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
                MoveItem(original, observableCollection, foundIndex, i);
                continue;
            }

            // Not found: insert new item at i
            original.Insert(i, newItem);
        }

        // Remove any extra trailing items from the destination
        while (original.Count > numberOfNew)
        {
            removedItems?.Add(original[^1]);
            original.RemoveAt(original.Count - 1);
        }
    }

    /// <summary>
    /// Moves an item from <paramref name="fromIndex"/> to <paramref name="toIndex"/>.
    /// Uses ObservableCollection.Move() when available for a single notification,
    /// otherwise falls back to RemoveAt + Insert.
    /// </summary>
    private static void MoveItem<T>(
        IList<T> original,
        ObservableCollection<T>? observableCollection,
        int fromIndex,
        int toIndex)
    {
        if (observableCollection is not null)
        {
            observableCollection.Move(fromIndex, toIndex);
        }
        else
        {
            var item = original[fromIndex];
            original.RemoveAt(fromIndex);
            original.Insert(toIndex, item);
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
