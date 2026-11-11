// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class TrayPaletteViewModelTests
{
    private sealed partial class TestProvider : CommandProvider
    {
        public TestProvider(string id)
        {
            Id = id;
        }

        public ICommandItem Item { get; init; } = new CommandItem(new AnonymousCommand(() => { }) { Id = "command" })
        {
            Title = "Quick action",
        };

        public bool IncludeTopLevelCommand { get; init; }

        public Func<string, ICommandItem?>? Lookup { get; init; }

        public int LookupCount { get; private set; }

        public override ICommandItem[] TopLevelCommands() => IncludeTopLevelCommand ? [Item] : [];

        public override ICommandItem? GetCommandItem(string id)
        {
            LookupCount++;
            return Lookup is { } lookup ? lookup(id) : id == "command" ? Item : null;
        }
    }

    [TestMethod]
    public async Task ResolvesPinsInOrderAndPreservesProviderContext()
    {
        var first = new TestProvider("first");
        var second = new TestProvider("second");
        var settings = new SettingsModel
        {
            TrayPalette = new() { Commands = [new("second", "command"), new("missing", "command"), new("first", "missing"), new("first", "command")] },
        };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        service.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        using var services = new ServiceCollection()
            .AddSingleton(service.Object)
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton<IContextMenuFactory>(DefaultContextMenuFactory.Instance)
            .BuildServiceProvider();
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([first, second], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.HasCount(2, model.Items);
        Assert.AreEqual("second", model.Items[0].Pin.ProviderId);
        Assert.AreSame(second.Item, model.Items[0].Item.Model.Unsafe);
        Assert.AreEqual(model.Items[0].Pin, model.Items[0].PageContext.Pin);
        Assert.IsTrue(model.Items[0].Item.PageContext.TryGetTarget(out var context));
        Assert.AreSame(model.Items[0].PageContext, context);
        Assert.AreSame(first.Item, model.Items[1].Item.Model.Unsafe);
        Assert.AreEqual("second", model.Items[0].CreateMessage().Context?.ProviderContext.ProviderId);
        Assert.IsNotNull(model.Items[0].CreateMessage().Context?.ExtensionHost);
        Assert.IsFalse(model.HasLoadError);
        Assert.IsFalse(model.IsEmpty);
        Assert.IsFalse(model.IsLoading);

        var original = model.Items[0];
        var changes = 0;
        model.Items.CollectionChanged += (_, _) => changes++;
        await model.RefreshAsync();
        Assert.AreSame(original, model.Items[0]);
        Assert.AreEqual(0, changes, "Reopening must not reset the grid or recreate its tiles.");

        manager.TopLevelCommands.Clear();
        changes = 0;
        NotifyCollectionChangedAction? action = null;
        model.Items.CollectionChanged += (_, e) => action = e.Action;
        await model.RefreshAsync();
        Assert.AreNotSame(original, model.Items[0], "Provider command changes must invalidate cached tiles.");
        Assert.AreEqual(1, changes, "Replacing the tiles must raise only one collection notification.");
        Assert.AreEqual(NotifyCollectionChangedAction.Reset, action);

        model.Items.Move(1, 0);
        model.SaveOrder();
        Assert.AreEqual("first", settings.TrayPalette.Commands[0].ProviderId);
        Assert.AreEqual("missing", settings.TrayPalette.Commands[1].ProviderId);
        Assert.AreEqual("second", settings.TrayPalette.Commands[3].ProviderId);

        await model.RefreshAsync();
        Assert.AreEqual("first", model.Items[0].Pin.ProviderId);
        settings = settings with { TrayPalette = settings.TrayPalette.Unpin(model.Items[0].Pin) };
        await model.RefreshAsync();
        Assert.HasCount(1, model.Items);
        Assert.AreEqual("second", model.Items[0].Pin.ProviderId);
    }

    [TestMethod]
    public async Task DisabledProviderDoesNotResolvePins()
    {
        var provider = new TestProvider("disabled");
        var settings = new SettingsModel
        {
            TrayPalette = new() { Commands = [new("disabled", "command")] },
        };
        settings = settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem("disabled", new() { IsEnabled = false }),
        };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(settings);
        using var services = new ServiceCollection()
            .AddSingleton(service.Object)
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton<IContextMenuFactory>(DefaultContextMenuFactory.Instance)
            .BuildServiceProvider();
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.IsEmpty(model.Items);
        Assert.IsTrue(model.IsEmpty);
        Assert.IsFalse(model.HasLoadError);
        Assert.HasCount(1, settings.TrayPalette.Commands);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReleasesOwnedResolutionButPreservesBorrowedTopLevelItem(bool includeTopLevelCommand)
    {
        using var cleanupFinished = new SemaphoreSlim(0);
        var item = new Mock<ICommandItem>();
        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "command", Name = "Quick action" });
        item.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(_ => cleanupFinished.Release());
        var provider = new TestProvider("provider") { Item = item.Object, IncludeTopLevelCommand = includeTopLevelCommand };
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.HasCount(1, model.Items);
        Assert.AreEqual(includeTopLevelCommand ? 0 : 1, provider.LookupCount);
        var trayItem = model.Items[0];
        var topLevel = manager.LookupCommand("provider", "command");
        if (includeTopLevelCommand)
        {
            Assert.IsNotNull(topLevel);
            Assert.AreNotSame(topLevel.ItemViewModel, trayItem.Item);
            Assert.AreNotSame(topLevel.ItemViewModel.Command, trayItem.Item.Command);
        }

        model.Dispose();

        Assert.IsTrue(await cleanupFinished.WaitAsync(TimeSpan.FromSeconds(2)), "The Tray presentation must be cleaned up.");
        if (includeTopLevelCommand)
        {
            item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            Assert.AreSame(topLevel, manager.LookupCommand("provider", "command"));
            Assert.AreEqual("Quick action", topLevel!.ItemViewModel.Title);
        }
        else
        {
            Assert.IsTrue(await cleanupFinished.WaitAsync(TimeSpan.FromSeconds(2)), "The owned resolution must also be cleaned up.");
            item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Exactly(2));
        }
    }

    [TestMethod]
    public async Task ResolvesGeneratedTopLevelIdWithoutProviderLookup()
    {
        var provider = new TestProvider("provider")
        {
            IncludeTopLevelCommand = true,
            Item = new CommandItem(new NoOpCommand { Name = "Generated identity" }),
        };
        var settings = new SettingsModel();
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        var command = manager.TopLevelCommands[0];
        settings = settings with { TrayPalette = new() { Commands = [new("provider", command.Id)] } };
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.HasCount(1, model.Items);
        Assert.AreEqual(command.Id, model.Items[0].Pin.CommandId);
        Assert.AreEqual(0, provider.LookupCount);
        Assert.IsFalse(model.HasLoadError);
    }

    [TestMethod]
    public async Task RejectsMismatchedProviderResult()
    {
        var provider = new TestProvider("provider")
        {
            Item = new CommandItem(new NoOpCommand { Id = "different-command", Name = "Wrong command" }),
        };
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);

        await model.RefreshAsync();

        Assert.IsEmpty(model.Items);
        Assert.AreEqual(1, provider.LookupCount);
        Assert.HasCount(1, settings.TrayPalette.Commands);
    }

    [TestMethod]
    public async Task DisablingLoadedProviderInvalidatesCachedTiles()
    {
        var provider = new TestProvider("provider");
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        await model.RefreshAsync();
        Assert.HasCount(1, model.Items);

        settings = settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem("provider", new() { IsEnabled = false }),
        };
        await model.RefreshAsync();

        Assert.IsEmpty(model.Items);
        Assert.AreEqual(1, provider.LookupCount, "A disabled provider must not be queried again.");
        Assert.IsFalse(model.HasLoadError);
    }

    [TestMethod]
    public async Task ReplacesItemsWithOneResetAndStandardPropertyNotifications()
    {
        var provider = new TestProvider("provider");
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        var resets = 0;
        var countChanges = 0;
        var indexerChanges = 0;
        ((INotifyPropertyChanged)model.Items).PropertyChanged += (_, e) =>
        {
            countChanges += e.PropertyName == "Count" ? 1 : 0;
            indexerChanges += e.PropertyName == "Item[]" ? 1 : 0;
        };
        model.Items.CollectionChanged += (_, e) =>
        {
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, e.Action);
            resets++;
            Assert.AreEqual(settings.TrayPalette.Commands.Count, model.Items.Count);
        };

        await model.RefreshAsync();
        settings = settings with { TrayPalette = new() { Commands = [] } };
        await model.RefreshAsync();

        Assert.AreEqual(2, resets);
        Assert.AreEqual(2, countChanges);
        Assert.AreEqual(2, indexerChanges);
        Assert.IsTrue(model.IsEmpty);
    }

    [TestMethod]
    public async Task RefreshDoesNotWaitForExtensionCleanup()
    {
        using var releaseCleanup = new ManualResetEventSlim();
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new Mock<ICommandItem>();
        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "command", Name = "Quick action" });
        item.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(_ =>
            {
                cleanupStarted.TrySetResult();
                releaseCleanup.Wait(TimeSpan.FromSeconds(10));
                cleanupFinished.TrySetResult();
            });
        var provider = new TestProvider("provider") { Item = item.Object, IncludeTopLevelCommand = true };
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        await model.RefreshAsync();
        var trayItem = model.Items[0];

        try
        {
            settings = settings with { TrayPalette = new() { Commands = [] } };
            await model.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.IsFalse(cleanupFinished.Task.IsCompleted, "Extension cleanup must still be blocked.");
            Assert.IsFalse(model.IsLoading, "The grid must finish refreshing without waiting for an extension RPC.");
            Assert.IsEmpty(model.Items);
            trayItem.Dispose();
        }
        finally
        {
            releaseCleanup.Set();
            await cleanupFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SlowExtensionDoesNotBlockOtherPinsAndLateItemsAreCleanedUp(bool includeTopLevelCommand)
    {
        using var releaseLoad = new ManualResetEventSlim();
        using var cleanupFinished = new SemaphoreSlim(0);
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockTitle = false;
        var item = new Mock<ICommandItem>();
        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "command", Name = "Slow action" });
        item.SetupGet(i => i.Title).Returns(() =>
        {
            if (Volatile.Read(ref blockTitle))
            {
                loadStarted.TrySetResult();
                releaseLoad.Wait(TimeSpan.FromSeconds(10));
            }

            return "Slow action";
        });
        item.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(_ => cleanupFinished.Release());
        var slow = new TestProvider("slow")
        {
            Item = item.Object,
            IncludeTopLevelCommand = includeTopLevelCommand,
            Lookup = _ =>
            {
                loadStarted.TrySetResult();
                releaseLoad.Wait(TimeSpan.FromSeconds(10));
                return item.Object;
            },
        };
        var healthy = new TestProvider("healthy");
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("slow", "command"), new("healthy", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([slow, healthy], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        Volatile.Write(ref blockTitle, includeTopLevelCommand);

        try
        {
            var refresh = model.RefreshAsync(TimeSpan.FromMilliseconds(250));
            await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await refresh.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.HasCount(1, model.Items);
            Assert.AreEqual("healthy", model.Items[0].Pin.ProviderId);
            Assert.IsTrue(model.HasLoadError);
            Assert.IsFalse(model.IsLoading);
            Assert.HasCount(2, settings.TrayPalette.Commands, "A timeout must not remove the saved pin.");
        }
        finally
        {
            releaseLoad.Set();
            Assert.IsTrue(await cleanupFinished.WaitAsync(TimeSpan.FromSeconds(5)), "A late result must release its extension subscriptions.");
        }

        item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    public async Task OlderRefreshCannotReplaceNewerItems()
    {
        using var releaseLoad = new ManualResetEventSlim();
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new TestProvider("slow")
        {
            Lookup = _ =>
            {
                loadStarted.TrySetResult();
                releaseLoad.Wait(TimeSpan.FromSeconds(10));
                return new CommandItem(new NoOpCommand { Id = "command", Name = "Old action" });
            },
        };
        var healthy = new TestProvider("healthy");
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("slow", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([slow, healthy], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        var olderRefresh = model.RefreshAsync();
        try
        {
            await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            settings = settings with { TrayPalette = new() { Commands = [new("healthy", "command")] } };
            await model.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var currentItem = model.Items[0];
            releaseLoad.Set();
            await olderRefresh.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.HasCount(1, model.Items);
            Assert.AreSame(currentItem, model.Items[0]);
            Assert.AreEqual("healthy", model.Items[0].Pin.ProviderId);
            Assert.IsFalse(model.IsLoading);
        }
        finally
        {
            releaseLoad.Set();
            await olderRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static ServiceProvider CreateServices(ISettingsService settingsService) => new ServiceCollection()
        .AddSingleton(settingsService)
        .AddSingleton(TaskScheduler.Default)
        .AddSingleton<IContextMenuFactory>(DefaultContextMenuFactory.Instance)
        .BuildServiceProvider();
}
