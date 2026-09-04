// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
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
public sealed partial class DockLabelPersistenceTests
{
    private const string CommandId = "test.dock.labels";
    private const string ProviderId = "test";
    private const string MonitorId = "test-monitor";

    private sealed class TestSettingsService(SettingsModel settings) : ISettingsService
    {
        public SettingsModel Settings { get; private set; } = settings;

        public event TypedEventHandler<ISettingsService, SettingsModel>? SettingsChanged;

        public void Reset(SettingsModel settings)
        {
            Settings = settings;
        }

        public void Save(bool hotReload = true)
        {
            UpdateSettings(settings => settings, hotReload);
        }

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

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(false, true, true)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void SaveBandOrder_NewBandPersistsLabelVisibility(
        bool defaultVisibility,
        bool showTitles,
        bool showSubtitles)
    {
        var pendingBand = CreateBandSettings();
        var localDockSettings = CreateDockSettings(defaultVisibility, ImmutableList.Create(pendingBand));
        var settingsService = new TestSettingsService(new SettingsModel { DockSettings = localDockSettings });

        using var serviceProvider = CreateServiceProvider(settingsService);
        using var commandManager = new TopLevelCommandManager(serviceProvider, []);
        using var dock = new DockViewModel(
            commandManager,
            DefaultContextMenuFactory.Instance,
            TaskScheduler.Default,
            settingsService);

        settingsService.Reset(new SettingsModel
        {
            DockSettings = CreateDockSettings(defaultVisibility, ImmutableList<DockBandSettings>.Empty),
        });
        dock.SnapshotBandOrder();

        var (band, root) = CreateBandViewModel(pendingBand, settingsService);
        try
        {
            dock.StartItems.Add(band);
            band.SnapshotShowLabels();
            band.ShowTitles = showTitles;
            band.ShowSubtitles = showSubtitles;

            dock.SaveBandOrder();
            settingsService.UpdateSettings(settings => settings with { ShowAppDetails = !settings.ShowAppDetails });

            var savedBand = settingsService.Settings.DockSettings.StartBands.Single();
            Assert.AreEqual(showTitles, savedBand.ShowTitles);
            Assert.AreEqual(showSubtitles, savedBand.ShowSubtitles);
            Assert.AreEqual(showTitles, savedBand.ResolveShowTitles(defaultVisibility));
            Assert.AreEqual(showSubtitles, savedBand.ResolveShowSubtitles(defaultVisibility));
        }
        finally
        {
            band.SafeCleanup();
            root.SafeCleanup();
        }
    }

    [TestMethod]
    public void SaveBandOrder_ExistingBandUsesLatestPersistedLabelVisibility()
    {
        var staleBand = CreateBandSettings(showTitles: false, showSubtitles: false);
        var localDockSettings = CreateDockSettings(true, ImmutableList.Create(staleBand));
        var settingsService = new TestSettingsService(new SettingsModel { DockSettings = localDockSettings });

        using var serviceProvider = CreateServiceProvider(settingsService);
        using var commandManager = new TopLevelCommandManager(serviceProvider, []);
        using var dock = new DockViewModel(
            commandManager,
            DefaultContextMenuFactory.Instance,
            TaskScheduler.Default,
            settingsService);

        var (band, root) = CreateBandViewModel(staleBand, settingsService);
        try
        {
            dock.StartItems.Add(band);
            settingsService.Reset(new SettingsModel
            {
                DockSettings = CreateDockSettings(
                    true,
                    ImmutableList.Create(CreateBandSettings(showTitles: true, showSubtitles: false))),
            });

            dock.SnapshotBandOrder();
            dock.SaveBandOrder();

            var savedBand = settingsService.Settings.DockSettings.StartBands.Single();
            Assert.AreEqual(true, savedBand.ShowTitles);
            Assert.AreEqual(false, savedBand.ShowSubtitles);
        }
        finally
        {
            band.SafeCleanup();
            root.SafeCleanup();
        }
    }

    [TestMethod]
    public void SaveBandOrder_NewBandPersistsLabelVisibilityForCustomizedMonitor()
    {
        var pendingBand = CreateBandSettings();
        var localDockSettings = CreateDockSettings(
            true,
            ImmutableList<DockBandSettings>.Empty,
            CreateMonitorConfig(ImmutableList.Create(pendingBand)));
        var settingsService = new TestSettingsService(new SettingsModel { DockSettings = localDockSettings });

        using var serviceProvider = CreateServiceProvider(settingsService);
        using var commandManager = new TopLevelCommandManager(serviceProvider, []);
        using var dock = new DockViewModel(
            commandManager,
            DefaultContextMenuFactory.Instance,
            TaskScheduler.Default,
            settingsService,
            MonitorId);

        settingsService.Reset(new SettingsModel
        {
            DockSettings = CreateDockSettings(
                true,
                ImmutableList<DockBandSettings>.Empty,
                CreateMonitorConfig(ImmutableList<DockBandSettings>.Empty)),
        });
        dock.SnapshotBandOrder();

        var (band, root) = CreateBandViewModel(pendingBand, settingsService);
        try
        {
            dock.StartItems.Add(band);
            band.SnapshotShowLabels();
            band.ShowTitles = false;
            band.ShowSubtitles = false;

            dock.SaveBandOrder();

            var monitor = settingsService.Settings.DockSettings.MonitorConfigs.Single();
            var savedBand = monitor.StartBands!.Single();
            Assert.AreEqual(false, savedBand.ShowTitles);
            Assert.AreEqual(false, savedBand.ShowSubtitles);
        }
        finally
        {
            band.SafeCleanup();
            root.SafeCleanup();
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
        bool showLabels,
        ImmutableList<DockBandSettings> startBands,
        DockMonitorConfig? monitorConfig = null)
    {
        return new DockSettings
        {
            ShowLabels = showLabels,
            StartBands = startBands,
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
            MonitorConfigs = monitorConfig is null
                ? ImmutableList<DockMonitorConfig>.Empty
                : ImmutableList.Create(monitorConfig),
        };
    }

    private static DockMonitorConfig CreateMonitorConfig(ImmutableList<DockBandSettings> startBands)
    {
        return new DockMonitorConfig
        {
            MonitorDeviceId = MonitorId,
            IsCustomized = true,
            StartBands = startBands,
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };
    }

    private static DockBandSettings CreateBandSettings(bool? showTitles = null, bool? showSubtitles = null)
    {
        return new DockBandSettings
        {
            ProviderId = ProviderId,
            CommandId = CommandId,
            ShowTitles = showTitles,
            ShowSubtitles = showSubtitles,
        };
    }

    private static (DockBandViewModel Band, CommandItemViewModel Root) CreateBandViewModel(
        DockBandSettings settings,
        ISettingsService settingsService)
    {
        var context = new TestPageContext();
        var page = new TestListPage
        {
            Id = CommandId,
            Name = "Label persistence test",
            Title = "Label persistence test",
        };
        var root = new CommandItemViewModel(
            new(new CommandItem(page) { Title = page.Title }),
            new(context),
            DefaultContextMenuFactory.Instance);
        root.SlowInitializeProperties();

        return (
            new DockBandViewModel(
                root,
                new(context),
                settings,
                settingsService,
                DefaultContextMenuFactory.Instance),
            root);
    }
}
