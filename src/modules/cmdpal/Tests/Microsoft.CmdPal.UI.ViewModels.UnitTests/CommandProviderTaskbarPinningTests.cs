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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class CommandProviderTaskbarPinningTests
{
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void PinDockBand_TaskbarPersistsLabelOptions(bool showTitles, bool showSubtitles)
    {
        var fixture = new TestFixture();

        fixture.Wrapper.PinDockBand("shared", fixture.Services, true, DockPinSide.Taskbar, showTitles, showSubtitles);

        var band = fixture.Settings.DockSettings.TaskbarBands.Single();
        Assert.AreEqual("shared", band.CommandId);
        Assert.AreEqual("test.provider", band.ProviderId);
        Assert.AreEqual(showTitles, band.ShowTitles);
        Assert.AreEqual(showSubtitles, band.ShowSubtitles);
        Assert.IsEmpty(fixture.Settings.DockSettings.StartBands);
        Assert.IsEmpty(fixture.Settings.DockSettings.CenterBands);
        Assert.IsEmpty(fixture.Settings.DockSettings.EndBands);
        Assert.AreEqual(1, fixture.ReloadCount);
    }

    [TestMethod]
    [DataRow(DockPinSide.Start)]
    [DataRow(DockPinSide.Center)]
    [DataRow(DockPinSide.End)]
    public void PinDockBand_TaskbarAllowsDockPinButRejectsTaskbarDuplicate(DockPinSide side)
    {
        var fixture = new TestFixture();
        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, side, true, true);
        var dockSettings = fixture.Settings.DockSettings;

        fixture.Wrapper.PinDockBand("shared", fixture.Services, true, DockPinSide.Taskbar, false, false);
        fixture.Wrapper.PinDockBand("shared", fixture.Services, true, DockPinSide.Taskbar, true, true);

        var band = fixture.Settings.DockSettings.TaskbarBands.Single();
        Assert.IsFalse(band.ShowTitles);
        Assert.IsFalse(band.ShowSubtitles);
        Assert.AreSame(dockSettings.StartBands, fixture.Settings.DockSettings.StartBands);
        Assert.AreSame(dockSettings.CenterBands, fixture.Settings.DockSettings.CenterBands);
        Assert.AreSame(dockSettings.EndBands, fixture.Settings.DockSettings.EndBands);
        Assert.AreEqual(1, fixture.ReloadCount);
    }

    [TestMethod]
    public void PinDockBand_DockAllowsExistingTaskbarPin()
    {
        var fixture = new TestFixture();
        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, DockPinSide.Taskbar);

        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, DockPinSide.Start);

        Assert.HasCount(1, fixture.Settings.DockSettings.StartBands);
        Assert.HasCount(1, fixture.Settings.DockSettings.TaskbarBands);
        Assert.AreEqual(0, fixture.ReloadCount);
    }

    [TestMethod]
    public void PinDockBand_TaskbarIgnoresMonitorDestination()
    {
        var fixture = new TestFixture();
        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, monitorDeviceId: "monitor");
        var monitorConfigs = fixture.Settings.DockSettings.MonitorConfigs;

        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, DockPinSide.Taskbar, monitorDeviceId: "monitor");

        Assert.HasCount(1, fixture.Settings.DockSettings.TaskbarBands);
        Assert.AreSame(monitorConfigs, fixture.Settings.DockSettings.MonitorConfigs);
    }

    [TestMethod]
    public void PinDockBand_TaskbarDistinguishesProviders()
    {
        var fixture = new TestFixture();
        var otherBand = new DockBandSettings { ProviderId = "other.provider", CommandId = "shared" };
        fixture.Settings = fixture.Settings with
        {
            DockSettings = fixture.Settings.DockSettings with { TaskbarBands = ImmutableList.Create(otherBand) },
        };

        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, DockPinSide.Taskbar);

        Assert.HasCount(2, fixture.Settings.DockSettings.TaskbarBands);
        Assert.AreEqual(otherBand, fixture.Settings.DockSettings.TaskbarBands[0]);
        Assert.AreEqual("test.provider", fixture.Settings.DockSettings.TaskbarBands[1].ProviderId);
    }

    [TestMethod]
    [DataRow(DockPinSide.Start)]
    [DataRow(DockPinSide.Taskbar)]
    public void UnpinDockBand_PreservesOtherLocationAndProvider(DockPinSide side)
    {
        var fixture = new TestFixture();
        var otherBand = new DockBandSettings { ProviderId = "other.provider", CommandId = "shared" };
        fixture.Settings = fixture.Settings with
        {
            DockSettings = fixture.Settings.DockSettings with
            {
                StartBands = ImmutableList.Create(otherBand),
                TaskbarBands = ImmutableList.Create(otherBand),
            },
        };
        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, DockPinSide.Start);
        fixture.Wrapper.PinDockBand("shared", fixture.Services, false, DockPinSide.Taskbar);
        fixture.Wrapper.PinDockBand("monitor-only", fixture.Services, false, monitorDeviceId: "monitor");
        var monitorConfigs = fixture.Settings.DockSettings.MonitorConfigs;

        fixture.Wrapper.UnpinDockBand("shared", fixture.Services, true, side);

        var removedFrom = side == DockPinSide.Taskbar ? fixture.Settings.DockSettings.TaskbarBands : fixture.Settings.DockSettings.StartBands;
        var otherLocation = side == DockPinSide.Taskbar ? fixture.Settings.DockSettings.StartBands : fixture.Settings.DockSettings.TaskbarBands;
        Assert.AreEqual(otherBand, removedFrom.Single());
        Assert.HasCount(2, otherLocation);
        Assert.AreSame(monitorConfigs, fixture.Settings.DockSettings.MonitorConfigs);
        Assert.AreEqual(1, fixture.ReloadCount);
    }

    private sealed class TestFixture
    {
        public SettingsModel Settings { get; set; } = new()
        {
            DockSettings = new DockSettings
            {
                StartBands = ImmutableList<DockBandSettings>.Empty,
                CenterBands = ImmutableList<DockBandSettings>.Empty,
                EndBands = ImmutableList<DockBandSettings>.Empty,
                TaskbarBands = ImmutableList<DockBandSettings>.Empty,
            },
        };

        public CommandProviderWrapper Wrapper { get; }

        public IServiceProvider Services { get; }

        public int ReloadCount { get; private set; }

        public TestFixture()
        {
            var settingsService = new Mock<ISettingsService>();
            settingsService.Setup(s => s.Settings).Returns(() => Settings);
            settingsService.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false))
                .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => Settings = update(Settings));
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(ISettingsService))).Returns(settingsService.Object);
            Services = services.Object;

            var provider = new Mock<ICommandProvider>();
            provider.SetupGet(p => p.Id).Returns("test.provider");
            provider.SetupGet(p => p.DisplayName).Returns("Test provider");
            provider.SetupGet(p => p.Icon).Returns(new Microsoft.CommandPalette.Extensions.Toolkit.IconInfo(string.Empty));
            Wrapper = new CommandProviderWrapper(provider.Object, TaskScheduler.Default);
            Wrapper.CommandsChanged += (_, _) => ReloadCount++;
        }
    }
}
