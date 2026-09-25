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
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class TaskbarBandPinningTests
{
    [TestMethod]
    [DataRow(DockPinSide.Start)]
    [DataRow(DockPinSide.Center)]
    [DataRow(DockPinSide.End)]
    public void GetAvailableBandsToAdd_TaskbarIgnoresDockPins(DockPinSide dockSide)
    {
        using var fixture = new TestFixture();
        fixture.ViewModel.AddBandToSection(fixture.SharedBand, dockSide);
        fixture.ViewModel.AddBandToSection(fixture.TaskbarBand, DockPinSide.Taskbar);

        CollectionAssert.AreEquivalent(
            new[] { fixture.SharedBand, fixture.UnpinnedBand },
            fixture.ViewModel.GetAvailableBandsToAdd(taskbarOnly: true).ToArray());
        CollectionAssert.AreEqual(
            new[] { fixture.UnpinnedBand },
            fixture.ViewModel.GetAvailableBandsToAdd().ToArray());
    }

    [TestMethod]
    public void AddBandToSection_TaskbarAllowsDockPinButRejectsTaskbarDuplicate()
    {
        using var fixture = new TestFixture();
        fixture.PinSharedBandToBoth();
        fixture.ViewModel.AddBandToSection(fixture.SharedBand, DockPinSide.Taskbar);

        Assert.AreEqual(1, fixture.ViewModel.StartItems.Count);
        Assert.AreEqual(1, fixture.ViewModel.TaskbarItems.Count);
        Assert.AreNotSame(fixture.ViewModel.StartItems[0], fixture.ViewModel.TaskbarItems[0]);
        Assert.IsFalse(fixture.ViewModel.GetAvailableBandsToAdd(taskbarOnly: true).Contains(fixture.SharedBand));

        fixture.ViewModel.SaveBandOrder();

        Assert.AreEqual(fixture.SharedBand.Id, fixture.Settings.DockSettings.StartBands.Single().CommandId);
        Assert.AreEqual(fixture.SharedBand.Id, fixture.Settings.DockSettings.TaskbarBands.Single().CommandId);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UnpinBand_PreservesPinInOtherLocation(bool taskbar)
    {
        using var fixture = new TestFixture();
        fixture.PinSharedBandToBoth();
        var band = taskbar ? fixture.ViewModel.TaskbarItems[0] : fixture.ViewModel.StartItems[0];

        fixture.ViewModel.UnpinBand(band);
        fixture.ViewModel.SaveBandOrder();

        Assert.AreEqual(taskbar ? 1 : 0, fixture.Settings.DockSettings.StartBands.Count);
        Assert.AreEqual(taskbar ? 0 : 1, fixture.Settings.DockSettings.TaskbarBands.Count);
        Assert.AreEqual(taskbar, fixture.ViewModel.GetAvailableBandsToAdd(taskbarOnly: true).Contains(fixture.SharedBand));
    }

    [TestMethod]
    [DataRow(DockPinSide.Start)]
    [DataRow(DockPinSide.Taskbar)]
    public void SyncBandPosition_PreservesPinInOtherLocation(DockPinSide side)
    {
        using var fixture = new TestFixture();
        fixture.PinSharedBandToBoth();
        fixture.ViewModel.AddBandToSection(fixture.UnpinnedBand, side);
        var items = side == DockPinSide.Taskbar ? fixture.ViewModel.TaskbarItems : fixture.ViewModel.StartItems;
        var band = items[0];
        items.Move(0, 1);

        fixture.ViewModel.SyncBandPosition(band, side, 1);
        fixture.ViewModel.SaveBandOrder();

        var reordered = side == DockPinSide.Taskbar ? fixture.Settings.DockSettings.TaskbarBands : fixture.Settings.DockSettings.StartBands;
        var other = side == DockPinSide.Taskbar ? fixture.Settings.DockSettings.StartBands : fixture.Settings.DockSettings.TaskbarBands;
        Assert.AreEqual(fixture.SharedBand.Id, reordered[1].CommandId);
        Assert.AreEqual(fixture.SharedBand.Id, other.Single().CommandId);
    }

    [TestMethod]
    public void RestoreBandOrder_RestoresSeparateDockAndTaskbarInstances()
    {
        using var fixture = new TestFixture();
        fixture.PinSharedBandToBoth();
        fixture.ViewModel.SaveBandOrder();
        var dockBand = fixture.ViewModel.StartItems[0];
        var taskbarBand = fixture.ViewModel.TaskbarItems[0];
        fixture.ViewModel.SnapshotBandOrder();
        fixture.ViewModel.UnpinBand(taskbarBand);

        fixture.ViewModel.RestoreBandOrder();

        Assert.AreSame(dockBand, fixture.ViewModel.StartItems.Single());
        Assert.AreSame(taskbarBand, fixture.ViewModel.TaskbarItems.Single());
    }

    [TestMethod]
    public void MoveBandWithoutSaving_BetweenDockSectionsPreservesTaskbarPin()
    {
        using var fixture = new TestFixture();
        fixture.PinSharedBandToBoth();

        fixture.ViewModel.MoveBandWithoutSaving(fixture.ViewModel.StartItems[0], DockPinSide.Center, 0);
        fixture.ViewModel.SaveBandOrder();

        Assert.AreEqual(0, fixture.Settings.DockSettings.StartBands.Count);
        Assert.AreEqual(fixture.SharedBand.Id, fixture.Settings.DockSettings.CenterBands.Single().CommandId);
        Assert.AreEqual(fixture.SharedBand.Id, fixture.Settings.DockSettings.TaskbarBands.Single().CommandId);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RemoveBandById_PreservesPinInOtherLocation(bool taskbar)
    {
        using var fixture = new TestFixture();
        fixture.PinSharedBandToBoth();

        fixture.ViewModel.RemoveBandById(fixture.SharedBand.Id, taskbarOnly: taskbar);
        fixture.ViewModel.SaveBandOrder();

        Assert.AreEqual(taskbar ? 1 : 0, fixture.Settings.DockSettings.StartBands.Count);
        Assert.AreEqual(taskbar ? 0 : 1, fixture.Settings.DockSettings.TaskbarBands.Count);
    }

    private sealed class TestFixture : IDisposable, IPageContext
    {
        private readonly Mock<ISettingsService> _settingsService = new();
        private readonly Mock<IServiceProvider> _serviceProvider = new();
        private readonly TopLevelCommandManager _commandManager;

        public SettingsModel Settings { get; private set; } = new()
        {
            DockSettings = new DockSettings
            {
                StartBands = ImmutableList<DockBandSettings>.Empty,
                CenterBands = ImmutableList<DockBandSettings>.Empty,
                EndBands = ImmutableList<DockBandSettings>.Empty,
                TaskbarBands = ImmutableList<DockBandSettings>.Empty,
            },
        };

        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public DockViewModel ViewModel { get; }

        public TopLevelViewModel SharedBand { get; }

        public TopLevelViewModel TaskbarBand { get; }

        public TopLevelViewModel UnpinnedBand { get; }

        public TestFixture()
        {
            _settingsService.Setup(s => s.Settings).Returns(() => Settings);
            _settingsService.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), false))
                .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => Settings = update(Settings));
            _serviceProvider.Setup(s => s.GetService(typeof(ISettingsService))).Returns(_settingsService.Object);
            _serviceProvider.Setup(s => s.GetService(typeof(TaskScheduler))).Returns(Scheduler);
            _commandManager = new TopLevelCommandManager(_serviceProvider.Object, Array.Empty<IExtensionService>());
            SharedBand = CreateBand("shared");
            TaskbarBand = CreateBand("taskbar");
            UnpinnedBand = CreateBand("unpinned");
            ViewModel = new DockViewModel(_commandManager, DefaultContextMenuFactory.Instance, Scheduler, _settingsService.Object);
            ViewModel.SnapshotBandOrder();
        }

        public void ShowException(Exception ex, string? extensionHint = null) => throw new AssertFailedException(ex.Message);

        public void PinSharedBandToBoth()
        {
            ViewModel.AddBandToSection(SharedBand, DockPinSide.Start);
            ViewModel.AddBandToSection(SharedBand, DockPinSide.Taskbar);
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            _commandManager.Dispose();
        }

        private TopLevelViewModel CreateBand(string id)
        {
            var command = new CommandItem(new NoOpCommand { Id = id, Name = id });
            var item = new CommandItemViewModel(new(command), new(this), DefaultContextMenuFactory.Instance);
            item.FastInitializeProperties();
            var band = new TopLevelViewModel(
                item,
                TopLevelType.DockBand,
                CommandPaletteHost.Instance,
                ProviderContext,
                new ProviderSettings(),
                _serviceProvider.Object,
                command,
                DefaultContextMenuFactory.Instance);
            _commandManager.DockBands.Add(band);
            return band;
        }
    }
}
