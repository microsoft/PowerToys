// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class DockBandsCollectionTests
{
    private sealed class TestSettingsService : ISettingsService
    {
        public SettingsModel Settings { get; private set; } = new();

        public event TypedEventHandler<ISettingsService, SettingsModel>? SettingsChanged;

        public void Save(bool hotReload = true) => UpdateSettings(s => s, hotReload);

        public void UpdateSettings(Func<SettingsModel, SettingsModel> transform, bool hotReload = true)
        {
            Settings = transform(Settings);
            if (hotReload)
            {
                SettingsChanged?.Invoke(this, Settings);
            }
        }
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private sealed partial class TestListPage : ListPage
    {
        public override IListItem[] GetItems() => [];
    }

    [TestMethod]
    public void AddRange_RaisesSingleNotification()
    {
        using var serviceProvider = CreateServiceProvider();
        var collection = new DockBandsCollection();
        var events = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => events.Add(e.Action);

        var a = CreateTopLevel("a", serviceProvider);
        var b = CreateTopLevel("b", serviceProvider);
        var c = CreateTopLevel("c", serviceProvider);

        collection.AddRange([a, b, c]);
        collection.AddRange([]);

        CollectionAssert.AreEqual(new[] { a, b, c }, collection);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, events);
    }

    [TestMethod]
    public void RemoveRange_RaisesSingleNotification()
    {
        using var serviceProvider = CreateServiceProvider();
        var collection = new DockBandsCollection();
        var a = CreateTopLevel("a", serviceProvider);
        var b = CreateTopLevel("b", serviceProvider);
        var c = CreateTopLevel("c", serviceProvider);
        collection.AddRange([a, b, c]);

        var events = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => events.Add(e.Action);

        collection.RemoveRange([a, c]);
        collection.RemoveRange([a]);

        CollectionAssert.AreEqual(new[] { b }, collection);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, events);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddSingleton<ISettingsService>(new TestSettingsService())
            .AddSingleton<TaskScheduler>(TaskScheduler.Default)
            .BuildServiceProvider();
    }

    private static TopLevelViewModel CreateTopLevel(string id, IServiceProvider serviceProvider)
    {
        var page = new TestListPage { Id = id, Name = id, Title = id };
        var item = new CommandItem(page) { Title = id };
        var itemViewModel = new CommandItemViewModel(new(item), new(new TestPageContext()), DefaultContextMenuFactory.Instance);
        itemViewModel.SlowInitializeProperties();

        return new TopLevelViewModel(
            itemViewModel,
            TopLevelType.DockBand,
            CommandPaletteHost.Instance,
            CommandProviderContext.Empty,
            new ProviderSettings(),
            serviceProvider,
            item,
            DefaultContextMenuFactory.Instance);
    }
}
