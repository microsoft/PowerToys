// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class CommandProviderWrapperDockPinTests
{
    private const string ProviderId = "dock-pin-test-provider";
    private const string CommandId = "new-pin";

    private static readonly MonitorInfo Monitor = new()
    {
        DeviceId = @"\\.\DISPLAY1",
        StableId = "connected-monitor",
        DisplayName = "Connected monitor",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = true,
    };

    [TestMethod]
    [DataRow(DockPinSide.Start, false)]
    [DataRow(DockPinSide.Center, false)]
    [DataRow(DockPinSide.End, false)]
    [DataRow(DockPinSide.Start, true)]
    [DataRow(DockPinSide.Center, true)]
    [DataRow(DockPinSide.End, true)]
    public void SingleMonitor_CustomizedLayout_PinAppearsInVisibleBands(DockPinSide side, bool alreadyInSharedList)
    {
        var config = new DockMonitorConfig { MonitorDeviceId = Monitor.StableId.ToUpperInvariant() }
            .ForkFromGlobal(new DockSettings { StartBands = ImmutableList.Create(Band("existing-monitor-pin")) });
        var dock = new DockSettings
        {
            CenterBands = alreadyInSharedList ? ImmutableList.Create(Band(CommandId)) : ImmutableList<DockBandSettings>.Empty,
            MonitorConfigs = ImmutableList.Create(config),
        };
        using var services = CreateServices(dock, new[] { Monitor });
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        wrapper.PinDockBand(CommandId, services, withReload: false, side: side, showTitles: false, showSubtitles: true);

        var updated = services.GetRequiredService<ISettingsService>().Settings.DockSettings;
        var updatedConfig = updated.MonitorConfigs.Single();
        var pinned = ResolvedBands(updated, updatedConfig, side).SingleOrDefault(b => b.CommandId == CommandId && b.ProviderId == ProviderId);
        Assert.IsNotNull(pinned);
        Assert.AreEqual(false, pinned.ShowTitles);
        Assert.AreEqual(true, pinned.ShowSubtitles);
        Assert.IsTrue(updatedConfig.StartBands!.Contains(Band("existing-monitor-pin")));
        Assert.AreEqual(dock with { MonitorConfigs = updated.MonitorConfigs }, updated);
    }

    [TestMethod]
    [DataRow(DockPinSide.Start)]
    [DataRow(DockPinSide.Center)]
    [DataRow(DockPinSide.End)]
    public void SingleMonitor_SharedLayout_UpdatesSharedBandsWithoutForking(DockPinSide side)
    {
        var config = new DockMonitorConfig { MonitorDeviceId = Monitor.StableId };
        var dock = new DockSettings { MonitorConfigs = ImmutableList.Create(config) };
        using var services = CreateServices(dock, new[] { Monitor });
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        wrapper.PinDockBand(CommandId, services, withReload: false, side: side);

        var updated = services.GetRequiredService<ISettingsService>().Settings.DockSettings;
        Assert.IsTrue(ResolvedBands(updated, updated.MonitorConfigs.Single(), side).Contains(Band(CommandId)));
        Assert.AreEqual(config, updated.MonitorConfigs.Single());
    }

    [TestMethod]
    public void SingleMonitor_DisconnectedCustomLayout_IsNotTargeted()
    {
        var config = new DockMonitorConfig { MonitorDeviceId = "disconnected-monitor" }.ForkFromGlobal(new DockSettings());
        var dock = new DockSettings { MonitorConfigs = ImmutableList.Create(config) };
        using var services = CreateServices(dock, new[] { Monitor });
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        wrapper.PinDockBand(CommandId, services, withReload: false, side: DockPinSide.Center);

        var updated = services.GetRequiredService<ISettingsService>().Settings.DockSettings;
        Assert.IsTrue(updated.CenterBands.Contains(Band(CommandId)));
        Assert.AreEqual(config, updated.MonitorConfigs.Single());
    }

    [TestMethod]
    public void MultipleMonitors_NoTarget_UpdatesSharedBands()
    {
        var config = new DockMonitorConfig { MonitorDeviceId = Monitor.StableId }.ForkFromGlobal(new DockSettings());
        var dock = new DockSettings { MonitorConfigs = ImmutableList.Create(config) };
        var secondMonitor = Monitor with { StableId = "second-monitor", IsPrimary = false };
        using var services = CreateServices(dock, new[] { Monitor, secondMonitor });
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        wrapper.PinDockBand(CommandId, services, withReload: false, side: DockPinSide.Center);

        var updated = services.GetRequiredService<ISettingsService>().Settings.DockSettings;
        Assert.IsTrue(updated.CenterBands.Contains(Band(CommandId)));
        Assert.AreEqual(config, updated.MonitorConfigs.Single());
    }

    [TestMethod]
    public void MultipleMonitors_ExplicitTarget_ForksThatMonitorsLayout()
    {
        var primaryConfig = new DockMonitorConfig { MonitorDeviceId = Monitor.StableId }.ForkFromGlobal(new DockSettings());
        var secondMonitor = Monitor with { StableId = "second-monitor", IsPrimary = false };
        var secondConfig = new DockMonitorConfig { MonitorDeviceId = secondMonitor.StableId };
        var dock = new DockSettings { MonitorConfigs = ImmutableList.Create(primaryConfig, secondConfig) };
        using var services = CreateServices(dock, new[] { Monitor, secondMonitor });
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        wrapper.PinDockBand(CommandId, services, withReload: false, side: DockPinSide.Center, monitorDeviceId: secondMonitor.StableId);

        var updated = services.GetRequiredService<ISettingsService>().Settings.DockSettings;
        Assert.AreEqual(primaryConfig, updated.MonitorConfigs[0]);
        Assert.IsTrue(updated.MonitorConfigs[1].IsCustomized);
        Assert.IsTrue(updated.MonitorConfigs[1].CenterBands!.Contains(Band(CommandId)));
        Assert.AreEqual(dock with { MonitorConfigs = updated.MonitorConfigs }, updated);
    }

    [TestMethod]
    public void SingleMonitor_AlreadyVisible_DoesNotDuplicatePin()
    {
        var config = new DockMonitorConfig { MonitorDeviceId = Monitor.StableId }
            .ForkFromGlobal(new DockSettings { EndBands = ImmutableList.Create(Band(CommandId)) });
        var dock = new DockSettings { MonitorConfigs = ImmutableList.Create(config) };
        using var services = CreateServices(dock, new[] { Monitor });
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        wrapper.PinDockBand(CommandId, services, withReload: false, side: DockPinSide.Start);

        Assert.AreEqual(dock, services.GetRequiredService<ISettingsService>().Settings.DockSettings);
    }

    private static DockBandSettings Band(string commandId) => new() { ProviderId = ProviderId, CommandId = commandId };

    private static ImmutableList<DockBandSettings> ResolvedBands(DockSettings dock, DockMonitorConfig config, DockPinSide side) => side switch
    {
        DockPinSide.Center => config.ResolveCenterBands(dock.CenterBands),
        DockPinSide.End => config.ResolveEndBands(dock.EndBands),
        _ => config.ResolveStartBands(dock.StartBands),
    };

    private static ServiceProvider CreateServices(DockSettings dock, IReadOnlyList<MonitorInfo> monitors)
    {
        var settings = new SettingsModel { DockSettings = dock };
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(() => settings);
        settingsService.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, _) => settings = transform(settings));
        var monitorService = new Mock<IMonitorService>();
        monitorService.Setup(s => s.GetMonitors(false)).Returns(monitors);

        return new ServiceCollection()
            .AddSingleton(settingsService.Object)
            .AddSingleton(monitorService.Object)
            .BuildServiceProvider();
    }

    private sealed partial class TestCommandProvider : CommandProvider
    {
        public TestCommandProvider()
        {
            Id = ProviderId;
            DisplayName = "Dock pin test provider";
        }

        public override ICommandItem[] TopLevelCommands() => Array.Empty<ICommandItem>();
    }
}
