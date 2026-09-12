// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ListHelpersReconciliationTests
{
    private sealed class Item(int value)
    {
        public int Value { get; } = value;

        public int EqualityCalls { get; private set; }

        public int HashCalls { get; private set; }

        public override bool Equals(object? obj)
        {
            EqualityCalls++;
            return obj is Item other && Value == other.Value;
        }

        public override int GetHashCode()
        {
            HashCalls++;
            return Value;
        }
    }

    private static List<Item> Update(IList<Item> original, IEnumerable<Item> newContents, bool trackRemoved)
    {
        if (trackRemoved)
        {
            ListHelpers.InPlaceUpdateList(original, newContents, out var removed);
            return removed;
        }

        ListHelpers.InPlaceUpdateList(original, newContents);
        return [];
    }

    [TestMethod]
    [DataRow(false, 3)]
    [DataRow(true, 3)]
    [DataRow(false, 5000)]
    [DataRow(true, 5000)]
    public void RetainedRotation_MovesOriginalInstances(bool trackRemoved, int count)
    {
        var items = Enumerable.Range(0, count).Select(i => new Item(i)).ToArray();
        var original = new ObservableCollection<Item>(items);
        var newItems = new[] { new Item(count - 1) }.Concat(Enumerable.Range(0, count - 1).Select(i => new Item(i)));
        var notifications = new List<NotifyCollectionChangedEventArgs>();
        original.CollectionChanged += (_, e) => notifications.Add(e);

        var removed = Update(original, newItems, trackRemoved);

        Assert.AreEqual(count, original.Count);
        Assert.AreSame(items[^1], original[0]);
        for (var i = 1; i < count; i++)
        {
            Assert.AreSame(items[i - 1], original[i]);
        }

        Assert.AreEqual(0, removed.Count);
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual(NotifyCollectionChangedAction.Move, notifications[0].Action);
        Assert.AreEqual(count - 1, notifications[0].OldStartingIndex);
        Assert.AreEqual(0, notifications[0].NewStartingIndex);
        Assert.AreSame(items[^1], notifications[0].NewItems![0]);
    }

    [TestMethod]
    [DataRow(false, 64)]
    [DataRow(true, 64)]
    [DataRow(false, 512)]
    [DataRow(true, 512)]
    public void DisjointResults_UseLinearComparisonsAndSingleReplacements(bool trackRemoved, int count)
    {
        var oldItems = Enumerable.Range(0, count).Select(i => new Item(i)).ToArray();
        var newItems = Enumerable.Range(count, count).Select(i => new Item(i)).ToArray();
        var original = new ObservableCollection<Item>(oldItems);
        var notifications = new List<NotifyCollectionChangedEventArgs>();
        original.CollectionChanged += (_, e) => notifications.Add(e);

        var removed = Update(original, newItems, trackRemoved);

        var equalityCalls = oldItems.Sum(i => i.EqualityCalls) + newItems.Sum(i => i.EqualityCalls);
        var hashCalls = oldItems.Sum(i => i.HashCalls) + newItems.Sum(i => i.HashCalls);
        Assert.IsTrue(equalityCalls <= 2 * count, $"Expected linear equality work, got {equalityCalls} calls for {count} rows.");
        Assert.IsTrue(hashCalls <= 3 * count, $"Expected linear hashing work, got {hashCalls} calls for {count} rows.");
        Assert.AreEqual(count, original.Count);
        Assert.AreEqual(count, notifications.Count);
        Assert.AreEqual(trackRemoved ? count : 0, removed.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.AreSame(newItems[i], original[i]);
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, notifications[i].Action);
            Assert.AreEqual(i, notifications[i].NewStartingIndex);
            Assert.AreSame(oldItems[i], notifications[i].OldItems![0]);
            Assert.AreSame(newItems[i], notifications[i].NewItems![0]);
            if (trackRemoved)
            {
                Assert.AreSame(oldItems[i], removed[i]);
            }
        }
    }

    [TestMethod]
    [DataRow(false, 0, 0)]
    [DataRow(true, 0, 0)]
    [DataRow(false, 0, 3)]
    [DataRow(true, 0, 3)]
    [DataRow(false, 3, 0)]
    [DataRow(true, 3, 0)]
    [DataRow(false, 2, 4)]
    [DataRow(true, 2, 4)]
    [DataRow(false, 4, 2)]
    [DataRow(true, 4, 2)]
    public void DisjointSizeChanges_NotifyOnlyChangedSlots(bool trackRemoved, int oldCount, int newCount)
    {
        var oldItems = Enumerable.Range(0, oldCount).Select(i => new Item(i)).ToArray();
        var newItems = Enumerable.Range(oldCount, newCount).Select(i => new Item(i)).ToArray();
        var original = new ObservableCollection<Item>(oldItems);
        var notifications = new List<NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        var removed = Update(original, newItems, trackRemoved);

        Assert.AreEqual(newCount, original.Count);
        for (var i = 0; i < newCount; i++)
        {
            Assert.AreSame(newItems[i], original[i]);
        }

        var sharedLength = Math.Min(oldCount, newCount);
        var expectedNotifications = Enumerable.Repeat(NotifyCollectionChangedAction.Replace, sharedLength)
            .Concat(Enumerable.Repeat(
                oldCount > newCount ? NotifyCollectionChangedAction.Remove : NotifyCollectionChangedAction.Add,
                Math.Abs(oldCount - newCount)))
            .ToArray();
        CollectionAssert.AreEqual(expectedNotifications, notifications);
        Assert.AreEqual(trackRemoved ? oldCount : 0, removed.Count);
        if (trackRemoved)
        {
            foreach (var item in oldItems)
            {
                Assert.IsTrue(removed.Any(removedItem => ReferenceEquals(item, removedItem)));
            }
        }
    }

    [TestMethod]
    [DataRow(false, 3, 3)]
    [DataRow(true, 3, 3)]
    [DataRow(false, 2, 4)]
    [DataRow(true, 2, 4)]
    [DataRow(false, 4, 2)]
    [DataRow(true, 4, 2)]
    public void PrefixOnlyChanges_PreserveInstancesWithoutHashing(bool trackRemoved, int oldCount, int newCount)
    {
        var oldItems = Enumerable.Range(0, oldCount).Select(i => new Item(i)).ToArray();
        var newItems = Enumerable.Range(0, newCount).Select(i => new Item(i)).ToArray();
        var original = new ObservableCollection<Item>(oldItems);
        var notifications = new List<NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        var removed = Update(original, newItems, trackRemoved);

        Assert.AreEqual(0, oldItems.Sum(i => i.HashCalls) + newItems.Sum(i => i.HashCalls));
        Assert.AreEqual(newCount, original.Count);
        for (var i = 0; i < newCount; i++)
        {
            Assert.AreSame(i < oldCount ? oldItems[i] : newItems[i], original[i]);
        }

        var expectedNotifications = Enumerable.Repeat(
            oldCount > newCount ? NotifyCollectionChangedAction.Remove : NotifyCollectionChangedAction.Add,
            Math.Abs(oldCount - newCount)).ToArray();
        CollectionAssert.AreEqual(expectedNotifications, notifications);
        Assert.AreEqual(trackRemoved ? Math.Max(0, oldCount - newCount) : 0, removed.Count);
        for (var i = 0; i < removed.Count; i++)
        {
            Assert.AreSame(oldItems[oldCount - i - 1], removed[i]);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DuplicateValues_ReorderAndReuseEachOriginalOccurrence(bool trackRemoved)
    {
        var first = new Item(1);
        var second = new Item(1);
        var last = new Item(2);
        var original = new ObservableCollection<Item> { first, second, last };
        var notifications = new List<NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        var removed = Update(original, [new Item(2), new Item(1), new Item(1)], trackRemoved);

        Assert.AreEqual(3, original.Count);
        Assert.AreSame(last, original[0]);
        Assert.AreSame(first, original[1]);
        Assert.AreSame(second, original[2]);
        Assert.AreEqual(0, removed.Count);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Move }, notifications);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DuplicateValues_ShrinkAndGrowWithoutReplacingRetainedInstances(bool trackRemoved)
    {
        var first = new Item(1);
        var surplus = new Item(1);
        var last = new Item(2);
        var added = new Item(1);
        var original = new ObservableCollection<Item> { first, surplus, last };
        var notifications = new List<NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        var removed = Update(original, [new Item(2), new Item(1)], trackRemoved);

        Assert.AreEqual(2, original.Count);
        Assert.AreSame(last, original[0]);
        Assert.AreSame(first, original[1]);
        Assert.AreEqual(trackRemoved ? 1 : 0, removed.Count);
        if (trackRemoved)
        {
            Assert.AreSame(surplus, removed[0]);
        }

        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Remove }, notifications);
        notifications.Clear();

        removed = Update(original, [new Item(2), new Item(1), added], trackRemoved);

        Assert.AreEqual(3, original.Count);
        Assert.AreSame(last, original[0]);
        Assert.AreSame(first, original[1]);
        Assert.AreSame(added, original[2]);
        Assert.AreEqual(0, removed.Count);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Add }, notifications);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RepeatedReferences_PreserveMultiplicity(bool trackRemoved)
    {
        var repeated = new Item(1);
        var last = new Item(2);
        var original = new ObservableCollection<Item> { repeated, repeated, last };

        var removed = Update(original, [last, repeated, repeated, repeated], trackRemoved);

        Assert.AreEqual(4, original.Count);
        Assert.AreSame(last, original[0]);
        Assert.IsTrue(original.Skip(1).All(item => ReferenceEquals(repeated, item)));
        Assert.AreEqual(0, removed.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NullEntries_PreserveRequestedOrderAndMultiplicity(bool trackRemoved)
    {
        var first = new Item(1);
        var last = new Item(2);
        var original = new ObservableCollection<Item> { first, null!, last, null! };
        var notifications = new List<NotifyCollectionChangedAction>();
        original.CollectionChanged += (_, e) => notifications.Add(e.Action);

        Update(original, [last, null!, first, null!], trackRemoved);

        Assert.AreEqual(4, original.Count);
        Assert.AreSame(last, original[0]);
        Assert.IsNull(original[1]);
        Assert.AreSame(first, original[2]);
        Assert.IsNull(original[3]);
        Assert.IsFalse(notifications.Contains(NotifyCollectionChangedAction.Reset));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PlainList_ReorderPreservesOriginalInstances(bool trackRemoved)
    {
        var items = new[] { new Item(1), new Item(2), new Item(3) };
        var original = new List<Item>(items);

        var removed = Update(original, [new Item(3), new Item(1)], trackRemoved);

        Assert.AreEqual(2, original.Count);
        Assert.AreSame(items[2], original[0]);
        Assert.AreSame(items[0], original[1]);
        Assert.AreEqual(trackRemoved ? 1 : 0, removed.Count);
        if (trackRemoved)
        {
            Assert.AreSame(items[1], removed[0]);
        }
    }
}
