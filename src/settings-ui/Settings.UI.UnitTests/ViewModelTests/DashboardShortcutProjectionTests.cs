// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public class DashboardShortcutProjectionTests
{
    private static readonly int[] EmptyStateCounts = [2, 1, 0, 1, 2, 3];

    [TestMethod]
    public void InitialRefresh_FiltersItemsAndModulesInSourceOrder()
    {
        var shortcut = new DashboardModuleShortcutItem { Label = "Shortcut", Shortcut = new List<object> { 65 } };
        var activation = new DashboardModuleActivationItem { Label = "Activation", Activation = "Double press" };
        var colorPicker = Module(ModuleType.ColorPicker, shortcut, new DashboardModuleButtonItem(), activation, new DashboardModuleTextItem());
        var disabled = Module(ModuleType.AlwaysOnTop, new DashboardModuleShortcutItem());
        disabled.UpdateStatus(false);
        var fancyZones = Module(ModuleType.FancyZones, new DashboardModuleShortcutItem());
        var modules = new[]
        {
            colorPicker,
            disabled,
            Module(ModuleType.Hosts, new DashboardModuleButtonItem()),
            Module(ModuleType.Peek),
            fancyZones,
        };
        var rows = new ObservableCollection<DashboardListItem>();
        var events = ObserveCollection(rows);

        DashboardShortcutProjection.Refresh(modules, rows);

        AssertOrder(rows, ModuleType.ColorPicker, ModuleType.FancyZones);
        CollectionAssert.AreEqual(new DashboardModuleItem[] { shortcut, activation }, rows[0].DashboardModuleItems.ToArray());
        Assert.AreNotSame(colorPicker, rows[0]);
        Assert.AreNotSame(colorPicker.DashboardModuleItems, rows[0].DashboardModuleItems);
        Assert.AreEqual(colorPicker.Icon, rows[0].Icon);
        Assert.AreEqual(colorPicker.Label, rows[0].Label);
        Assert.AreEqual(colorPicker.IsLocked, rows[0].IsLocked);
        Assert.IsTrue(rows[0].IsEnabled);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add }, events.Select(e => e.Action).ToArray());
    }

    [TestMethod]
    public void EquivalentRefresh_PreservesRowsIconsAndInnerCollectionsWithoutNotifications()
    {
        var modules = ThreeModules();
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(modules, rows);
        var originals = rows.ToArray();
        var innerCollections = rows.Select(row => row.DashboardModuleItems).ToArray();
        var icons = rows.Select(row => row.Icon).ToArray();
        var events = ObserveCollection(rows);
        var properties = rows.Select(ObserveProperties).ToArray();
        var innerEvents = innerCollections.Select(ObserveCollection).ToArray();

        for (int refresh = 0; refresh < 25; refresh++)
        {
            DashboardShortcutProjection.Refresh(modules, rows);
        }

        Assert.AreEqual(0, events.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            Assert.AreSame(originals[i], rows[i]);
            Assert.AreSame(icons[i], rows[i].Icon);
            Assert.AreSame(innerCollections[i], rows[i].DashboardModuleItems);
            Assert.AreSame(modules[i].DashboardModuleItems[0], rows[i].DashboardModuleItems[0]);
            Assert.AreEqual(0, properties[i].Count);
            Assert.AreEqual(0, innerEvents[i].Count);
        }
    }

    [TestMethod]
    public void RepeatedToggleAndEcho_OnlyAddsOrRemovesTheToggledModule()
    {
        var modules = ThreeModules();
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(modules, rows);
        var first = rows[0];
        var last = rows[2];
        var firstItems = first.DashboardModuleItems;
        var lastItems = last.DashboardModuleItems;
        var events = ObserveCollection(rows);
        var firstProperties = ObserveProperties(first);
        var lastProperties = ObserveProperties(last);
        var firstItemEvents = ObserveCollection(firstItems);
        var lastItemEvents = ObserveCollection(lastItems);
        var countChanges = 0;
        ((INotifyPropertyChanged)rows).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(rows.Count))
            {
                countChanges++;
            }
        };

        for (int cycle = 0; cycle < 10; cycle++)
        {
            modules[1].UpdateStatus(false);
            DashboardShortcutProjection.Refresh(modules, rows);
            DashboardShortcutProjection.Refresh(modules, rows);
            AssertOrder(rows, ModuleType.AlwaysOnTop, ModuleType.FancyZones);
            modules[1].UpdateStatus(true);
            DashboardShortcutProjection.Refresh(modules, rows);
            DashboardShortcutProjection.Refresh(modules, rows);
            AssertOrder(rows, ModuleType.AlwaysOnTop, ModuleType.ColorPicker, ModuleType.FancyZones);
            Assert.AreSame(first, rows[0]);
            Assert.AreSame(last, rows[2]);
            Assert.AreSame(firstItems, rows[0].DashboardModuleItems);
            Assert.AreSame(lastItems, rows[2].DashboardModuleItems);
        }

        Assert.AreEqual(20, events.Count);
        Assert.AreEqual(10, events.Count(e => e.Action == NotifyCollectionChangedAction.Add));
        Assert.AreEqual(10, events.Count(e => e.Action == NotifyCollectionChangedAction.Remove));
        Assert.AreEqual(20, countChanges);
        foreach (var change in events)
        {
            var changed = (DashboardListItem)(change.NewItems ?? change.OldItems)[0];
            Assert.AreEqual(ModuleType.ColorPicker, changed.Tag);
            Assert.AreEqual(1, change.NewStartingIndex >= 0 ? change.NewStartingIndex : change.OldStartingIndex);
        }

        Assert.AreEqual(0, firstProperties.Count + lastProperties.Count);
        Assert.AreEqual(0, firstItemEvents.Count + lastItemEvents.Count);
    }

    [TestMethod]
    public void SourceReordering_MovesExistingRowsWithoutCountChanges()
    {
        var modules = ThreeModules();
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(modules, rows);
        var originals = rows.ToArray();
        var events = ObserveCollection(rows);
        var properties = ObserveProperties(rows);

        DashboardShortcutProjection.Refresh(new[] { modules[2], modules[0], modules[1] }, rows);

        CollectionAssert.AreEqual(new[] { originals[2], originals[0], originals[1] }, rows.ToArray());
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(NotifyCollectionChangedAction.Move, events[0].Action);
        Assert.AreEqual(2, events[0].OldStartingIndex);
        Assert.AreEqual(0, events[0].NewStartingIndex);
        Assert.IsFalse(properties.Contains(nameof(rows.Count)));
    }

    [TestMethod]
    public void RemovingAllModules_NotifiesEmptyCountAndRepopulatesWithoutReset()
    {
        var modules = ThreeModules();
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(modules, rows);
        var original = rows[0];
        var counts = new List<int>();
        ((INotifyPropertyChanged)rows).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(rows.Count))
            {
                counts.Add(rows.Count);
            }
        };
        var events = ObserveCollection(rows);

        DashboardShortcutProjection.Refresh(Array.Empty<DashboardListItem>(), rows);
        Assert.AreEqual(0, rows.Count);
        DashboardShortcutProjection.Refresh(Array.Empty<DashboardListItem>(), rows);
        DashboardShortcutProjection.Refresh(modules, rows);

        CollectionAssert.AreEqual(EmptyStateCounts, counts);
        Assert.AreEqual(6, events.Count);
        Assert.IsFalse(events.Any(e => e.Action == NotifyCollectionChangedAction.Reset));
        Assert.AreNotSame(original, rows[0], "Removed rows must not be retained in a cache.");
        AssertOrder(rows, ModuleType.AlwaysOnTop, ModuleType.ColorPicker, ModuleType.FancyZones);
    }

    [TestMethod]
    public void ReplacementSource_UpdatesMetadataAndCallbackOnTheExistingRow()
    {
        var source = Module(ModuleType.ColorPicker, new DashboardModuleShortcutItem());
        var oldCallbacks = 0;
        var newCallbacks = 0;
        source.EnabledChangedCallback = _ => oldCallbacks++;
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        var row = rows[0];
        var innerCollection = row.DashboardModuleItems;
        var properties = ObserveProperties(row);
        var events = ObserveCollection(rows);
        var replacement = Module(ModuleType.ColorPicker, source.DashboardModuleItems[0]);
        replacement.Label = "Updated label";
        replacement.Icon = "Updated.svg";
        replacement.IsLocked = true;
        replacement.EnabledChangedCallback = _ => newCallbacks++;

        DashboardShortcutProjection.Refresh(new[] { replacement }, rows);

        Assert.AreSame(row, rows[0]);
        Assert.AreSame(innerCollection, row.DashboardModuleItems);
        Assert.AreEqual(replacement.Label, row.Label);
        Assert.AreEqual(replacement.Icon, row.Icon);
        Assert.IsTrue(row.IsLocked);
        CollectionAssert.AreEquivalent(new[] { nameof(row.Label), nameof(row.Icon), nameof(row.IsLocked) }, properties);
        Assert.AreEqual(0, events.Count);
        Assert.AreEqual(0, oldCallbacks + newCallbacks);
        row.IsEnabled = false;
        Assert.AreEqual(0, oldCallbacks);
        Assert.AreEqual(1, newCallbacks);
    }

    [TestMethod]
    public void EquivalentReplacementSource_DoesNotNotifyOrDuplicateRows()
    {
        var source = Module(ModuleType.ColorPicker, new DashboardModuleShortcutItem());
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        var original = rows[0];
        var properties = ObserveProperties(original);
        var events = ObserveCollection(rows);
        var innerEvents = ObserveCollection(original.DashboardModuleItems);
        var replacement = Module(ModuleType.ColorPicker, source.DashboardModuleItems[0]);

        DashboardShortcutProjection.Refresh(new[] { replacement }, rows);

        Assert.AreEqual(1, rows.Count);
        Assert.AreSame(original, rows[0]);
        Assert.AreEqual(0, events.Count + innerEvents.Count + properties.Count);
    }

    [TestMethod]
    public void ReplacementSourceWithNewItems_UpdatesOnlyItsRetainedInnerCollection()
    {
        var modules = ThreeModules();
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(modules, rows);
        var original = rows[1];
        var inner = original.DashboardModuleItems;
        var events = ObserveCollection(rows);
        var firstEvents = ObserveCollection(rows[0].DashboardModuleItems);
        var lastEvents = ObserveCollection(rows[2].DashboardModuleItems);
        var shortcut = new DashboardModuleShortcutItem { Label = "New shortcut", Shortcut = new List<object> { 66 } };
        var activation = new DashboardModuleActivationItem { Label = "New activation", Activation = "Hold" };
        modules[1] = Module(ModuleType.ColorPicker, shortcut, activation);

        DashboardShortcutProjection.Refresh(modules, rows);

        Assert.AreSame(original, rows[1]);
        Assert.AreSame(inner, rows[1].DashboardModuleItems);
        CollectionAssert.AreEqual(new DashboardModuleItem[] { shortcut, activation }, inner.ToArray());
        Assert.AreEqual(0, events.Count + firstEvents.Count + lastEvents.Count);
    }

    [TestMethod]
    public void ChangedItems_ReconcilesOnlyTheAffectedInnerCollection()
    {
        var first = new DashboardModuleShortcutItem();
        var removed = new DashboardModuleShortcutItem();
        var last = new DashboardModuleActivationItem();
        var added = new DashboardModuleShortcutItem { Shortcut = new List<object> { 66 } };
        var source = Module(ModuleType.ColorPicker, first, removed, last);
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        var row = rows[0];
        var inner = row.DashboardModuleItems;
        var events = ObserveCollection(rows);
        var innerEvents = ObserveCollection(inner);
        source.DashboardModuleItems = new ObservableCollection<DashboardModuleItem>
        {
            last,
            new DashboardModuleButtonItem(),
            first,
            added,
        };

        DashboardShortcutProjection.Refresh(new[] { source }, rows);

        Assert.AreSame(row, rows[0]);
        Assert.AreSame(inner, row.DashboardModuleItems);
        CollectionAssert.AreEqual(new DashboardModuleItem[] { last, first, added }, inner.ToArray());
        Assert.AreEqual(0, events.Count);
        CollectionAssert.AreEqual(
            new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Add },
            innerEvents.Select(e => e.Action).ToArray());
    }

    [TestMethod]
    public void ExistingItemPropertyChanges_NotifyRetainedBindingsWithoutCollectionChanges()
    {
        var shortcut = new DashboardModuleShortcutItem();
        var activation = new DashboardModuleActivationItem();
        var source = Module(ModuleType.ColorPicker, shortcut, activation);
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        var shortcutProperties = ObserveProperties(rows[0].DashboardModuleItems[0]);
        var activationProperties = ObserveProperties(rows[0].DashboardModuleItems[1]);
        var events = ObserveCollection(rows);
        var innerEvents = ObserveCollection(rows[0].DashboardModuleItems);
        var keys = new List<object> { 65 };

        shortcut.Label = "Changed shortcut";
        shortcut.Shortcut = keys;
        activation.Label = "Changed activation";
        activation.Activation = "Double press";
        DashboardShortcutProjection.Refresh(new[] { source }, rows);

        CollectionAssert.AreEqual(new[] { nameof(shortcut.Label), nameof(shortcut.Shortcut) }, shortcutProperties);
        CollectionAssert.AreEqual(new[] { nameof(activation.Label), nameof(activation.Activation) }, activationProperties);
        shortcutProperties.Clear();
        activationProperties.Clear();
        shortcut.Label = "Changed shortcut";
        shortcut.Shortcut = keys;
        activation.Label = "Changed activation";
        activation.Activation = "Double press";
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        Assert.AreEqual(0, shortcutProperties.Count + activationProperties.Count);
        Assert.AreEqual(0, events.Count + innerEvents.Count);
    }

    [TestMethod]
    public void LastShortcutRemoved_FiltersOutRowUntilAShortcutReturns()
    {
        var source = Module(ModuleType.ColorPicker, new DashboardModuleShortcutItem());
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        var events = ObserveCollection(rows);

        source.DashboardModuleItems = new ObservableCollection<DashboardModuleItem> { new DashboardModuleButtonItem() };
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        Assert.AreEqual(0, rows.Count);
        source.DashboardModuleItems.Add(new DashboardModuleActivationItem());
        DashboardShortcutProjection.Refresh(new[] { source }, rows);

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(1, rows[0].DashboardModuleItems.Count);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }, events.Select(e => e.Action).ToArray());
    }

    [TestMethod]
    public void ProgrammaticStatusRefresh_DoesNotInvokeEnabledCallback()
    {
        var source = Module(ModuleType.ColorPicker, new DashboardModuleShortcutItem());
        var callbacks = 0;
        source.EnabledChangedCallback = _ => callbacks++;
        var rows = new ObservableCollection<DashboardListItem>();
        DashboardShortcutProjection.Refresh(new[] { source }, rows);
        var row = rows[0];
        row.UpdateStatus(false);
        var properties = ObserveProperties(row);

        DashboardShortcutProjection.Refresh(new[] { source }, rows);

        Assert.AreSame(row, rows[0]);
        Assert.IsTrue(row.IsEnabled);
        Assert.AreEqual(0, callbacks);
        CollectionAssert.AreEqual(new[] { nameof(row.IsEnabled) }, properties);
        row.IsEnabled = false;
        Assert.AreEqual(1, callbacks, "User changes still reach the original callback.");
    }

    [TestMethod]
    public void SeparateOwners_DoNotShareProjectionRowsOrCollections()
    {
        var modules = ThreeModules();
        var firstOwner = new ObservableCollection<DashboardListItem>();
        var secondOwner = new ObservableCollection<DashboardListItem>();

        DashboardShortcutProjection.Refresh(modules, firstOwner);
        DashboardShortcutProjection.Refresh(modules, secondOwner);

        for (int i = 0; i < modules.Length; i++)
        {
            Assert.AreNotSame(firstOwner[i], secondOwner[i]);
            Assert.AreNotSame(firstOwner[i].DashboardModuleItems, secondOwner[i].DashboardModuleItems);
        }

        DashboardShortcutProjection.Refresh(Array.Empty<DashboardListItem>(), firstOwner);
        Assert.AreEqual(3, secondOwner.Count);
    }

    [TestMethod]
    public void DuplicateModuleTypes_AreRejectedWithoutAddingDuplicateRows()
    {
        var rows = new ObservableCollection<DashboardListItem>();
        var modules = new[]
        {
            Module(ModuleType.ColorPicker, new DashboardModuleShortcutItem()),
            Module(ModuleType.ColorPicker, new DashboardModuleActivationItem()),
        };

        Assert.ThrowsExactly<ArgumentException>(() => DashboardShortcutProjection.Refresh(modules, rows));
        Assert.AreEqual(0, rows.Count);
    }

    private static DashboardListItem Module(ModuleType type, params DashboardModuleItem[] items)
    {
        var module = new DashboardListItem
        {
            Tag = type,
            Label = type.ToString(),
            Icon = $"{type}.svg",
            DashboardModuleItems = new ObservableCollection<DashboardModuleItem>(items),
        };
        module.UpdateStatus(true);
        return module;
    }

    private static DashboardListItem[] ThreeModules() =>
    [
        Module(ModuleType.AlwaysOnTop, new DashboardModuleShortcutItem()),
        Module(ModuleType.ColorPicker, new DashboardModuleShortcutItem()),
        Module(ModuleType.FancyZones, new DashboardModuleActivationItem()),
    ];

    private static void AssertOrder(IEnumerable<DashboardListItem> rows, params ModuleType[] types)
        => CollectionAssert.AreEqual(types, rows.Select(row => row.Tag).ToArray());

    private static List<NotifyCollectionChangedEventArgs> ObserveCollection<T>(ObservableCollection<T> collection)
    {
        var events = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, e) => events.Add(e);
        return events;
    }

    private static List<string> ObserveProperties(INotifyPropertyChanged item)
    {
        var properties = new List<string>();
        item.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        return properties;
    }
}
