// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class DockBandLoadingTests
{
    private const string ProviderId = "test";

    private sealed class TestSettingsService(SettingsModel settings) : ISettingsService
    {
        public SettingsModel Settings { get; private set; } = settings;

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
    public async Task SetupBands_ReusesExistingBandsWhenMoreBandsLoad()
    {
        var settingsService = new TestSettingsService(new SettingsModel
        {
            DockSettings = CreateDockSettings(start: [CreateBandSettings("a"), CreateBandSettings("b")]),
        });
        using var serviceProvider = CreateServiceProvider(settingsService);
        using var commandManager = new TopLevelCommandManager(serviceProvider, []);
        using var dock = new DockViewModel(commandManager, DefaultContextMenuFactory.Instance, TaskScheduler.Default, settingsService);

        commandManager.DockBands.Add(CreateTopLevel("a", serviceProvider));
        await WaitUntilAsync(() => dock.StartItems.Count == 1);
        var bandA = dock.StartItems[0];

        commandManager.DockBands.Add(CreateTopLevel("b", serviceProvider));
        await WaitUntilAsync(() => dock.StartItems.Count == 2);

        Assert.AreSame(bandA, dock.StartItems[0]);
        Assert.AreEqual("b", dock.StartItems[1].Id);
    }

    [TestMethod]
    public async Task SetupBands_KeepsBandInstanceWhenMovedBetweenSections()
    {
        var bandSettings = CreateBandSettings("a");
        var settingsService = new TestSettingsService(new SettingsModel
        {
            DockSettings = CreateDockSettings(start: [bandSettings]),
        });
        using var serviceProvider = CreateServiceProvider(settingsService);
        using var commandManager = new TopLevelCommandManager(serviceProvider, []);
        using var dock = new DockViewModel(commandManager, DefaultContextMenuFactory.Instance, TaskScheduler.Default, settingsService);

        commandManager.DockBands.Add(CreateTopLevel("a", serviceProvider));
        await WaitUntilAsync(() => dock.StartItems.Count == 1);
        var bandA = dock.StartItems[0];

        dock.UpdateSettings(CreateDockSettings(end: [bandSettings]));
        await WaitUntilAsync(() => dock.EndItems.Count == 1 && dock.StartItems.Count == 0);

        Assert.AreSame(bandA, dock.EndItems[0]);
    }

    [TestMethod]
    public async Task SetupBands_RecreatesBandWhenItsSettingsChange()
    {
        var settingsService = new TestSettingsService(new SettingsModel
        {
            DockSettings = CreateDockSettings(start: [CreateBandSettings("a")]),
        });
        using var serviceProvider = CreateServiceProvider(settingsService);
        using var commandManager = new TopLevelCommandManager(serviceProvider, []);
        using var dock = new DockViewModel(commandManager, DefaultContextMenuFactory.Instance, TaskScheduler.Default, settingsService);

        commandManager.DockBands.Add(CreateTopLevel("a", serviceProvider));
        await WaitUntilAsync(() => dock.StartItems.Count == 1);
        var original = dock.StartItems[0];

        dock.UpdateSettings(CreateDockSettings(start: [CreateBandSettings("a") with { ShowTitles = false }]));
        await WaitUntilAsync(() => dock.StartItems.Count == 1 && !ReferenceEquals(original, dock.StartItems[0]));

        Assert.IsFalse(dock.StartItems[0].ShowTitles);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.Elapsed > TimeSpan.FromSeconds(5))
            {
                Assert.Fail("Timed out waiting for the dock to update.");
            }

            await Task.Delay(10);
        }
    }

    private static ServiceProvider CreateServiceProvider(ISettingsService settingsService)
    {
        return new ServiceCollection()
            .AddSingleton(settingsService)
            .AddSingleton<TaskScheduler>(TaskScheduler.Default)
            .BuildServiceProvider();
    }

    private static DockSettings CreateDockSettings(
        ImmutableList<DockBandSettings>? start = null,
        ImmutableList<DockBandSettings>? end = null)
    {
        return new DockSettings
        {
            StartBands = start ?? ImmutableList<DockBandSettings>.Empty,
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = end ?? ImmutableList<DockBandSettings>.Empty,
            MonitorConfigs = ImmutableList<DockMonitorConfig>.Empty,
        };
    }

    private static DockBandSettings CreateBandSettings(string commandId) =>
        new() { ProviderId = ProviderId, CommandId = commandId };

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
