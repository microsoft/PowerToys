// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ListHelpersInPlaceUpdateTests
{
    // Use a reference-type wrapper so tests work with the `where T : class` constraint
    // and we can verify identity (same instance) in removedItems tests.
    private sealed class Item(string name)
    {
        public string Name { get; } = name;

        public override string ToString() => Name;

        public override bool Equals(object? obj) => obj is Item other && Name == other.Name;

        public override int GetHashCode() => Name.GetHashCode();
    }

    private static Item[] MakeItems(params string[] names) =>
        names.Select(n => new Item(n)).ToArray();

    private static void AssertSequence(IList<Item> actual, params string[] expected)
    {
        var actualNames = actual.Select(i => i.Name).ToArray();
        CollectionAssert.AreEqual(expected, actualNames, $"Expected [{string.Join(", ", expected)}] but got [{string.Join(", ", actualNames)}]");
    }

    private static void AssertRemovedContainsExactly(List<Item> removedItems, IList<Item> originalItems, IList<Item> newItems)
    {
        // removedItems should contain exactly the items from original that are not in newItems
        var newSet = new HashSet<Item>(newItems);
        var expectedRemoved = originalItems.Where(i => !newSet.Contains(i)).ToList();

        // Same count
        Assert.AreEqual(expectedRemoved.Count, removedItems.Count, $"Expected {expectedRemoved.Count} removed items but got {removedItems.Count}");

        // Same instances (by reference, since we're checking cleanup correctness)
        foreach (var expected in expectedRemoved)
        {
            Assert.IsTrue(removedItems.Contains(expected), $"Expected '{expected.Name}' in removedItems but it was missing");
        }
    }

    [TestMethod]
    public void IdenticalLists_NoChanges()
    {
        var items = MakeItems("A", "B", "C");
        var original = new ObservableCollection<Item>(items);
        var newContents = items.ToList(); // same items, same order

        ListHelpers.InPlaceUpdateList(original, newContents, out var removed);

        AssertSequence(original, "A", "B", "C");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void EmptyToNonEmpty_AddsAll()
    {
        var original = new ObservableCollection<Item>();
        var newItems = MakeItems("A", "B", "C");

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "A", "B", "C");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void NonEmptyToEmpty_RemovesAll()
    {
        var items = MakeItems("A", "B", "C");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, [], out var removed);

        Assert.AreEqual(0, original.Count);
        Assert.AreEqual(3, removed.Count);
    }

    [TestMethod]
    public void SingleItem_Replace()
    {
        var a = new Item("A");
        var b = new Item("B");
        var original = new ObservableCollection<Item> { a };

        ListHelpers.InPlaceUpdateList(original, [b], out var removed);

        AssertSequence(original, "B");
        Assert.AreEqual(1, removed.Count);
        Assert.AreSame(a, removed[0]);
    }

    [TestMethod]
    public void FilterDown_RemovesNonMatching()
    {
        var items = MakeItems("A", "B", "C", "D", "E");
        var original = new ObservableCollection<Item>(items);
        var filtered = new[] { items[0], items[2], items[4] }; // A, C, E

        ListHelpers.InPlaceUpdateList(original, filtered, out var removed);

        AssertSequence(original, "A", "C", "E");
        Assert.AreEqual(2, removed.Count); // B, D removed
        AssertRemovedContainsExactly(removed, items, filtered);
    }

    [TestMethod]
    public void FilterDown_EveryOtherItem()
    {
        var items = MakeItems("A", "B", "C", "D", "E", "F", "G", "H");
        var original = new ObservableCollection<Item>(items);
        var filtered = new[] { items[1], items[3], items[5], items[7] }; // B, D, F, H

        ListHelpers.InPlaceUpdateList(original, filtered, out var removed);

        AssertSequence(original, "B", "D", "F", "H");
        Assert.AreEqual(4, removed.Count);
        AssertRemovedContainsExactly(removed, items, filtered);
    }

    [TestMethod]
    public void Expand_InsertsNewItems()
    {
        var items = MakeItems("A", "C", "E");
        var original = new ObservableCollection<Item>(items);
        var b = new Item("B");
        var d = new Item("D");
        var expanded = new[] { items[0], b, items[1], d, items[2] }; // A, B, C, D, E

        ListHelpers.InPlaceUpdateList(original, expanded, out var removed);

        AssertSequence(original, "A", "B", "C", "D", "E");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void Reversed_ReordersCorrectly()
    {
        var items = MakeItems("A", "B", "C", "D", "E");
        var original = new ObservableCollection<Item>(items);
        var reversed = items.Reverse().ToArray();

        ListHelpers.InPlaceUpdateList(original, reversed, out var removed);

        AssertSequence(original, "E", "D", "C", "B", "A");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void MoveFirstToLast()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new ObservableCollection<Item>(items);
        var reordered = new[] { items[1], items[2], items[3], items[0] }; // B, C, D, A

        ListHelpers.InPlaceUpdateList(original, reordered, out var removed);

        AssertSequence(original, "B", "C", "D", "A");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void MoveLastToFirst()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new ObservableCollection<Item>(items);
        var reordered = new[] { items[3], items[0], items[1], items[2] }; // D, A, B, C

        ListHelpers.InPlaceUpdateList(original, reordered, out var removed);

        AssertSequence(original, "D", "A", "B", "C");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void NoOverlap_ReplacesAll()
    {
        var oldItems = MakeItems("A", "B", "C");
        var newItems = MakeItems("X", "Y", "Z");
        var original = new ObservableCollection<Item>(oldItems);

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "X", "Y", "Z");
        Assert.AreEqual(3, removed.Count);
        AssertRemovedContainsExactly(removed, oldItems, newItems);
    }

    [TestMethod]
    public void NoOverlap_DifferentSizes_OriginalLarger()
    {
        var oldItems = MakeItems("A", "B", "C", "D", "E");
        var newItems = MakeItems("X", "Y");
        var original = new ObservableCollection<Item>(oldItems);

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "X", "Y");
        Assert.AreEqual(5, removed.Count);
    }

    [TestMethod]
    public void NoOverlap_DifferentSizes_NewLarger()
    {
        var oldItems = MakeItems("A", "B");
        var newItems = MakeItems("X", "Y", "Z", "W");
        var original = new ObservableCollection<Item>(oldItems);

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "X", "Y", "Z", "W");
        Assert.AreEqual(2, removed.Count);
    }

    [TestMethod]
    public void MixedRemoveAndReorder()
    {
        var items = MakeItems("A", "X", "Y", "C", "B");
        var original = new ObservableCollection<Item>(items);

        // Keep A, B, C but reorder; remove X, Y
        var newList = new[] { items[0], items[4], items[3] }; // A, B, C

        ListHelpers.InPlaceUpdateList(original, newList, out var removed);

        AssertSequence(original, "A", "B", "C");
        Assert.AreEqual(2, removed.Count);
        AssertRemovedContainsExactly(removed, items, newList);
    }

    [TestMethod]
    public void MixedAddRemoveReorder()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new ObservableCollection<Item>(items);
        var e = new Item("E");

        // Remove B, D; add E; reorder to C, A, E
        var newList = new[] { items[2], items[0], e }; // C, A, E

        ListHelpers.InPlaceUpdateList(original, newList, out var removed);

        AssertSequence(original, "C", "A", "E");
        Assert.AreEqual(2, removed.Count); // B, D
        AssertRemovedContainsExactly(removed, items, newList);
    }

    [TestMethod]
    public void ItemsBetweenCurrentAndFoundAreInNewList_NotRemovedIncorrectly()
    {
        // This is the scenario that caused the icon bug:
        // Items between the current position and the found target
        // appear later in newList and must NOT be put in removedItems.
        var items = MakeItems("A", "B", "C", "D", "E");
        var original = new ObservableCollection<Item>(items);

        // Reverse: items B, C, D are between position 0 and E's position
        // but all appear in newList
        var reversed = new[] { items[4], items[3], items[2], items[1], items[0] };

        ListHelpers.InPlaceUpdateList(original, reversed, out var removed);

        AssertSequence(original, "E", "D", "C", "B", "A");
        Assert.AreEqual(0, removed.Count, "No items should be removed when all items are reused");

        // Verify all original instances are still in the collection (not cleaned up)
        foreach (var item in items)
        {
            Assert.IsTrue(original.Contains(item), $"Item '{item.Name}' should still be in the collection (same instance)");
        }
    }

    [TestMethod]
    public void RemovedItems_NeverContainsItemsStillInNewList()
    {
        // Simulate the exact FetchItems scenario: reuse ViewModel instances
        var a = new Item("A");
        var b = new Item("B");
        var c = new Item("C");
        var d = new Item("D");
        var e = new Item("E");
        var original = new ObservableCollection<Item> { a, b, c, d, e };

        // New list reuses same instances but in different order, minus some
        var newList = new Item[] { e, c, a }; // reversed subset

        ListHelpers.InPlaceUpdateList(original, newList, out var removed);

        AssertSequence(original, "E", "C", "A");

        // Critical: removed should only contain b and d
        Assert.AreEqual(2, removed.Count);
        Assert.IsTrue(removed.Contains(b), "B should be in removedItems");
        Assert.IsTrue(removed.Contains(d), "D should be in removedItems");

        // Critical: removed must NOT contain items still in the list
        Assert.IsFalse(removed.Contains(a), "A is still in use — must not be in removedItems");
        Assert.IsFalse(removed.Contains(c), "C is still in use — must not be in removedItems");
        Assert.IsFalse(removed.Contains(e), "E is still in use — must not be in removedItems");
    }

    [TestMethod]
    public void WorksWithPlainList()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new List<Item>(items);
        var newList = new[] { items[2], items[0] }; // C, A

        ListHelpers.InPlaceUpdateList(original, newList, out var removed);

        AssertSequence(original, "C", "A");
        Assert.AreEqual(2, removed.Count);
    }

    [TestMethod]
    public void TwoArgOverload_ProducesCorrectResult()
    {
        var items = MakeItems("A", "B", "C");
        var original = new ObservableCollection<Item>(items);
        var newList = new[] { items[2], items[0] }; // C, A

        ListHelpers.InPlaceUpdateList(original, newList);

        AssertSequence(original, "C", "A");
    }

    [TestMethod]
    public void AcceptsLazyEnumerable()
    {
        var items = MakeItems("A", "B", "C");
        var original = new ObservableCollection<Item>(items);

        // Pass a lazy IEnumerable (not materialized)
        IEnumerable<Item> lazy = items.Reverse().Where(_ => true);

        ListHelpers.InPlaceUpdateList(original, lazy, out var removed);

        AssertSequence(original, "C", "B", "A");
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    public void IncrementalSearch_ProgressiveFiltering()
    {
        // Simulate typing a search query character by character
        var all = MakeItems("Apple", "Banana", "Avocado", "Blueberry", "Apricot");
        var original = new ObservableCollection<Item>(all);

        // First keystroke "A" — filter to A items
        var filtered1 = new[] { all[0], all[2], all[4] }; // Apple, Avocado, Apricot
        ListHelpers.InPlaceUpdateList(original, filtered1, out var removed1);
        AssertSequence(original, "Apple", "Avocado", "Apricot");
        Assert.AreEqual(2, removed1.Count);

        // Second keystroke "Ap" — filter further
        var filtered2 = new[] { all[0], all[4] }; // Apple, Apricot
        ListHelpers.InPlaceUpdateList(original, filtered2, out var removed2);
        AssertSequence(original, "Apple", "Apricot");
        Assert.AreEqual(1, removed2.Count);

        // Clear search — back to all
        ListHelpers.InPlaceUpdateList(original, all, out var removed3);
        AssertSequence(original, "Apple", "Banana", "Avocado", "Blueberry", "Apricot");
        Assert.AreEqual(0, removed3.Count);
    }

    [TestMethod]
    public void PageNavigation_CompleteReplacement()
    {
        // Simulate navigating from one extension page to another
        var page1 = MakeItems("P1A", "P1B", "P1C", "P1D");
        var page2 = MakeItems("P2A", "P2B", "P2C");
        var original = new ObservableCollection<Item>(page1);

        ListHelpers.InPlaceUpdateList(original, page2, out var removed1);
        AssertSequence(original, "P2A", "P2B", "P2C");
        Assert.AreEqual(4, removed1.Count);

        // Navigate back
        ListHelpers.InPlaceUpdateList(original, page1, out var removed2);
        AssertSequence(original, "P1A", "P1B", "P1C", "P1D");
        Assert.AreEqual(3, removed2.Count);
    }

    [TestMethod]
    public void StableItems_SameInstancePreserved()
    {
        var a = new Item("A");
        var b = new Item("B");
        var c = new Item("C");
        var original = new ObservableCollection<Item> { a, b, c };

        // Remove middle item
        ListHelpers.InPlaceUpdateList(original, [a, c]);

        Assert.AreSame(a, original[0], "A should be the same instance");
        Assert.AreSame(c, original[1], "C should be the same instance");
    }

    [TestMethod]
    public void ZeroOverlap_UsesReplaceNotInsertRemove()
    {
        // Track notifications to verify Replace path is used
        var oldItems = MakeItems("A", "B", "C");
        var newItems = MakeItems("X", "Y", "Z");
        var original = new ObservableCollection<Item>(oldItems);

        var notifications = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "X", "Y", "Z");
        Assert.AreEqual(3, removed.Count);

        // All notifications should be Replace (not Add/Remove pairs)
        Assert.IsTrue(
            notifications.All(a => a == System.Collections.Specialized.NotifyCollectionChangedAction.Replace),
            $"Expected all Replace but got: [{string.Join(", ", notifications)}]");
    }

    [TestMethod]
    public void ZeroOverlap_ShrinkingList_ReplaceThenRemove()
    {
        var oldItems = MakeItems("A", "B", "C", "D", "E");
        var newItems = MakeItems("X", "Y");
        var original = new ObservableCollection<Item>(oldItems);

        var notifications = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "X", "Y");
        Assert.AreEqual(5, removed.Count);

        // 2 Replace + 3 Remove
        var replaces = notifications.Count(a => a == System.Collections.Specialized.NotifyCollectionChangedAction.Replace);
        var removes = notifications.Count(a => a == System.Collections.Specialized.NotifyCollectionChangedAction.Remove);
        Assert.AreEqual(2, replaces);
        Assert.AreEqual(3, removes);
    }

    [TestMethod]
    public void ZeroOverlap_GrowingList_ReplaceThenAdd()
    {
        var oldItems = MakeItems("A", "B");
        var newItems = MakeItems("X", "Y", "Z", "W");
        var original = new ObservableCollection<Item>(oldItems);

        var notifications = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        ListHelpers.InPlaceUpdateList(original, newItems, out var removed);

        AssertSequence(original, "X", "Y", "Z", "W");
        Assert.AreEqual(2, removed.Count);

        // 2 Replace + 2 Add
        var replaces = notifications.Count(a => a == System.Collections.Specialized.NotifyCollectionChangedAction.Replace);
        var adds = notifications.Count(a => a == System.Collections.Specialized.NotifyCollectionChangedAction.Add);
        Assert.AreEqual(2, replaces);
        Assert.AreEqual(2, adds);
    }

    [TestMethod]
    public void TwoArg_IdenticalLists_NoChanges()
    {
        var items = MakeItems("A", "B", "C");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, items.ToList());

        AssertSequence(original, "A", "B", "C");
    }

    [TestMethod]
    public void TwoArg_EmptyToNonEmpty()
    {
        var original = new ObservableCollection<Item>();

        ListHelpers.InPlaceUpdateList(original, MakeItems("A", "B", "C"));

        AssertSequence(original, "A", "B", "C");
    }

    [TestMethod]
    public void TwoArg_NonEmptyToEmpty()
    {
        var original = new ObservableCollection<Item>(MakeItems("A", "B", "C"));

        ListHelpers.InPlaceUpdateList(original, Array.Empty<Item>());

        Assert.AreEqual(0, original.Count);
    }

    [TestMethod]
    public void TwoArg_FilterDown()
    {
        var items = MakeItems("A", "B", "C", "D", "E");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, new[] { items[0], items[2], items[4] });

        AssertSequence(original, "A", "C", "E");
    }

    [TestMethod]
    public void TwoArg_FilterDown_EveryOtherItem()
    {
        var items = MakeItems("A", "B", "C", "D", "E", "F", "G", "H");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, new[] { items[1], items[3], items[5], items[7] });

        AssertSequence(original, "B", "D", "F", "H");
    }

    [TestMethod]
    public void TwoArg_Expand()
    {
        var items = MakeItems("A", "C", "E");
        var original = new ObservableCollection<Item>(items);
        var b = new Item("B");
        var d = new Item("D");

        ListHelpers.InPlaceUpdateList(original, new[] { items[0], b, items[1], d, items[2] });

        AssertSequence(original, "A", "B", "C", "D", "E");
    }

    [TestMethod]
    public void TwoArg_Reversed()
    {
        var items = MakeItems("A", "B", "C", "D", "E");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, items.Reverse());

        AssertSequence(original, "E", "D", "C", "B", "A");
    }

    [TestMethod]
    public void TwoArg_MoveFirstToLast()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, new[] { items[1], items[2], items[3], items[0] });

        AssertSequence(original, "B", "C", "D", "A");
    }

    [TestMethod]
    public void TwoArg_MoveLastToFirst()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, new[] { items[3], items[0], items[1], items[2] });

        AssertSequence(original, "D", "A", "B", "C");
    }

    [TestMethod]
    public void TwoArg_NoOverlap_ReplacesAll()
    {
        var original = new ObservableCollection<Item>(MakeItems("A", "B", "C"));

        ListHelpers.InPlaceUpdateList(original, MakeItems("X", "Y", "Z"));

        AssertSequence(original, "X", "Y", "Z");
    }

    [TestMethod]
    public void TwoArg_NoOverlap_OriginalLarger()
    {
        var original = new ObservableCollection<Item>(MakeItems("A", "B", "C", "D", "E"));

        ListHelpers.InPlaceUpdateList(original, MakeItems("X", "Y"));

        AssertSequence(original, "X", "Y");
    }

    [TestMethod]
    public void TwoArg_NoOverlap_NewLarger()
    {
        var original = new ObservableCollection<Item>(MakeItems("A", "B"));

        ListHelpers.InPlaceUpdateList(original, MakeItems("X", "Y", "Z", "W"));

        AssertSequence(original, "X", "Y", "Z", "W");
    }

    [TestMethod]
    public void TwoArg_MixedRemoveAndReorder()
    {
        var items = MakeItems("A", "X", "Y", "C", "B");
        var original = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original, new[] { items[0], items[4], items[3] });

        AssertSequence(original, "A", "B", "C");
    }

    [TestMethod]
    public void TwoArg_MixedAddRemoveReorder()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new ObservableCollection<Item>(items);
        var e = new Item("E");

        ListHelpers.InPlaceUpdateList(original, new[] { items[2], items[0], e });

        AssertSequence(original, "C", "A", "E");
    }

    [TestMethod]
    public void TwoArg_IncrementalSearch()
    {
        var all = MakeItems("Apple", "Banana", "Avocado", "Blueberry", "Apricot");
        var original = new ObservableCollection<Item>(all);

        // "A" filter
        ListHelpers.InPlaceUpdateList(original, new[] { all[0], all[2], all[4] });
        AssertSequence(original, "Apple", "Avocado", "Apricot");

        // "Ap" filter
        ListHelpers.InPlaceUpdateList(original, new[] { all[0], all[4] });
        AssertSequence(original, "Apple", "Apricot");

        // Clear
        ListHelpers.InPlaceUpdateList(original, all);
        AssertSequence(original, "Apple", "Banana", "Avocado", "Blueberry", "Apricot");
    }

    [TestMethod]
    public void TwoArg_PageNavigation()
    {
        var page1 = MakeItems("P1A", "P1B", "P1C", "P1D");
        var page2 = MakeItems("P2A", "P2B", "P2C");
        var original = new ObservableCollection<Item>(page1);

        ListHelpers.InPlaceUpdateList(original, page2);
        AssertSequence(original, "P2A", "P2B", "P2C");

        ListHelpers.InPlaceUpdateList(original, page1);
        AssertSequence(original, "P1A", "P1B", "P1C", "P1D");
    }

    [TestMethod]
    public void TwoArg_WorksWithPlainList()
    {
        var items = MakeItems("A", "B", "C", "D");
        var original = new List<Item>(items);

        ListHelpers.InPlaceUpdateList(original, new[] { items[2], items[0] });

        AssertSequence(original, "C", "A");
    }

    [TestMethod]
    public void TwoArg_AcceptsLazyEnumerable()
    {
        var items = MakeItems("A", "B", "C");
        var original = new ObservableCollection<Item>(items);

        IEnumerable<Item> lazy = items.Reverse().Where(_ => true);
        ListHelpers.InPlaceUpdateList(original, lazy);

        AssertSequence(original, "C", "B", "A");
    }

    [TestMethod]
    public void TwoArg_SingleItemReplace()
    {
        var original = new ObservableCollection<Item> { new Item("A") };

        ListHelpers.InPlaceUpdateList(original, new[] { new Item("B") });

        AssertSequence(original, "B");
    }

    [TestMethod]
    public void BothOverloads_ProduceSameResult_FilterAndReorder()
    {
        var items = MakeItems("A", "B", "C", "D", "E", "F");
        var newList = new[] { items[4], items[2], items[0], new Item("G") }; // E, C, A, G

        var original1 = new ObservableCollection<Item>(items);
        var original2 = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original1, newList);
        ListHelpers.InPlaceUpdateList(original2, newList, out _);

        var names1 = original1.Select(i => i.Name).ToArray();
        var names2 = original2.Select(i => i.Name).ToArray();
        CollectionAssert.AreEqual(names1, names2, "Both overloads should produce identical results");
    }

    [TestMethod]
    public void BothOverloads_ProduceSameResult_CompleteReversal()
    {
        var items = MakeItems("A", "B", "C", "D", "E");
        var reversed = items.Reverse().ToArray();

        var original1 = new ObservableCollection<Item>(items);
        var original2 = new ObservableCollection<Item>(items);

        ListHelpers.InPlaceUpdateList(original1, reversed);
        ListHelpers.InPlaceUpdateList(original2, reversed, out _);

        var names1 = original1.Select(i => i.Name).ToArray();
        var names2 = original2.Select(i => i.Name).ToArray();
        CollectionAssert.AreEqual(names1, names2, "Both overloads should produce identical results");
    }

    [TestMethod]
    public void BothOverloads_ProduceSameResult_NoOverlap()
    {
        var oldItems = MakeItems("A", "B", "C", "D");
        var newItems = MakeItems("W", "X", "Y");

        var original1 = new ObservableCollection<Item>(oldItems);
        var original2 = new ObservableCollection<Item>(oldItems);

        ListHelpers.InPlaceUpdateList(original1, newItems);
        ListHelpers.InPlaceUpdateList(original2, newItems, out _);

        var names1 = original1.Select(i => i.Name).ToArray();
        var names2 = original2.Select(i => i.Name).ToArray();
        CollectionAssert.AreEqual(names1, names2, "Both overloads should produce identical results");
    }
}
