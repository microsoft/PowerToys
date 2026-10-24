// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings.UI.Library;

namespace ViewModelTests;

[TestClass]
public class ShellCollectionTests
{
    [TestMethod]
    [DataRow(typeof(PowerAccentShortcutControl), "Keys", typeof(List<object>))]
    [DataRow(typeof(ShortcutDialogContentControl), "Keys", typeof(List<object>))]
    [DataRow(typeof(SettingsPageControl), "PrimaryLinks", typeof(ObservableCollection<PageLink>))]
    [DataRow(typeof(SettingsPageControl), "SecondaryLinks", typeof(ObservableCollection<PageLink>))]
    public void DependencyPropertyWrappersRetainPublicSetterSignatures(Type controlType, string propertyName, Type collectionType)
    {
        // Inspect metadata without constructing WinUI controls outside a UI thread.
        var property = controlType.GetProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual(collectionType, property.PropertyType);
        var setter = property.SetMethod;

        Assert.IsNotNull(setter);
        Assert.IsTrue(setter.IsPublic);
        CollectionAssert.DoesNotContain(setter.ReturnParameter.GetRequiredCustomModifiers(), typeof(IsExternalInit));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SearchRefreshAndEmptyResultsKeepCollectionsAndNotifySubscribers(bool useNull)
    {
        var viewModel = new SearchResultsViewModel();
        var modules = viewModel.ModuleResults;
        var groups = viewModel.GroupedSettingsResults;
        var moduleNotifications = 0;
        var groupNotifications = 0;
        var noResultsNotifications = 0;
        modules.CollectionChanged += (_, _) => moduleNotifications++;
        groups.CollectionChanged += (_, _) => groupNotifications++;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SearchResultsViewModel.HasNoResults))
            {
                noResultsNotifications++;
            }
        };

        var module = new SettingEntry(EntryType.SettingsPage, "Module", "TestPage", null, null);
        var first = new SettingEntry(EntryType.SettingsCard, "First", "TestPage", "FirstCard", null);
        viewModel.SetSearchResults("first", new List<SettingEntry> { module, first });

        CollectionAssert.AreEqual(new List<SettingEntry> { module }, modules);
        CollectionAssert.AreEqual(new List<SettingEntry> { first }, groups.Single().Settings);
        Assert.IsFalse(viewModel.HasNoResults);

        var second = new SettingEntry(EntryType.SettingsCard, "Second", "TestPage", "SecondCard", null);
        viewModel.SetSearchResults("second", new List<SettingEntry> { second });

        Assert.AreEqual(0, modules.Count);
        CollectionAssert.AreEqual(new List<SettingEntry> { second }, groups.Single().Settings);

        viewModel.SetSearchResults("empty", useNull ? null : new List<SettingEntry>());

        Assert.AreSame(modules, viewModel.ModuleResults);
        Assert.AreSame(groups, viewModel.GroupedSettingsResults);
        Assert.AreEqual(0, modules.Count);
        Assert.AreEqual(0, groups.Count);
        Assert.IsTrue(viewModel.HasNoResults);
        Assert.IsTrue(moduleNotifications > 0);
        Assert.IsTrue(groupNotifications > 0);
        Assert.AreEqual(3, noResultsNotifications);
    }

    [TestMethod]
    public void DashboardItemsRetainInitializedCollectionsAndSupportReordering()
    {
        var keys = new List<object> { "Ctrl", "A" };
        var shortcut = new DashboardModuleShortcutItem { Shortcut = keys };
        var other = new DashboardModuleTextItem { Label = "Other" };
        var items = new ObservableCollection<DashboardModuleItem> { shortcut, other };
        var item = new DashboardListItem { DashboardModuleItems = items };
        var changes = 0;
        item.DashboardModuleItems.CollectionChanged += (_, _) => changes++;

        item.DashboardModuleItems.Move(1, 0);
        shortcut.Shortcut.Add("B");

        Assert.AreSame(items, item.DashboardModuleItems);
        Assert.AreSame(other, item.DashboardModuleItems[0]);
        Assert.AreSame(keys, shortcut.Shortcut);
        Assert.AreEqual(3, shortcut.Shortcut.Count);
        Assert.AreEqual(1, changes);
        Assert.AreEqual(0, new DashboardListItem().DashboardModuleItems.Count);
    }

    [TestMethod]
    public void NavigationAndConflictResponseKeepTheirConstructedCollections()
    {
        var results = new List<SettingEntry>();
        var navigation = new SearchResultsNavigationParams("query", results);
        var conflicts = new List<ModuleHotkeyData>();
        var response = new HotkeyConflictResponse { AllConflicts = conflicts };

        results.Add(new SettingEntry { Header = "Result" });
        conflicts.Add(new ModuleHotkeyData { ModuleName = "Test" });

        Assert.AreSame(results, navigation.Results);
        Assert.AreEqual(1, navigation.Results.Count);
        Assert.AreSame(conflicts, response.AllConflicts);
        Assert.AreEqual(1, response.AllConflicts.Count);
        Assert.AreEqual(0, new HotkeyConflictResponse().AllConflicts.Count);
    }
}
