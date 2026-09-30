// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockMonitorRenameTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);

    private static readonly MonitorInfo WirelessMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY14",
        StableId = @"\\?\DISPLAY#WIRELESS#UID256#{guid}",
        FriendlyName = "Wireless display",
        DisplayName = "Wireless display",
        Bounds = new ScreenRect(1920, 0, 3840, 1080),
        WorkArea = new ScreenRect(1920, 0, 3840, 1040),
        Dpi = 96,
        IsPrimary = false,
    };

    [TestMethod]
    public void DefaultName_DoesNotPersistHardwareNameOrPrimarySuffix()
    {
        var monitor = WirelessMonitor with { IsPrimary = true };
        var (viewModel, settingsService) = CreateEditor(Config(), monitor);

        Assert.AreEqual("Wireless display (Primary)", viewModel.DisplayName);
        Assert.AreEqual("Wireless display", viewModel.DefaultDisplayName);
        Assert.AreEqual(string.Empty, viewModel.DisplayNameInput);
        Assert.IsNull(settingsService.Object.Settings.DockSettings.MonitorConfigs[0].DisplayNameOverride);
        Assert.IsFalse(viewModel.SaveDisplayNameCommand.CanExecute(null));
        Assert.IsFalse(viewModel.ResetDisplayNameCommand.CanExecute(null));
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void Save_CommitsDraftOnceAndNotifiesDisplayName()
    {
        var (viewModel, settingsService) = CreateEditor(Config());
        var originalSettings = settingsService.Object.Settings;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.DisplayNameInput = "办公桌";
        viewModel.DisplayNameInput = "  办公桌右屏  ";

        Assert.AreEqual("Wireless display", viewModel.DisplayName);
        Assert.AreSame(originalSettings, settingsService.Object.Settings);
        Assert.IsTrue(viewModel.SaveDisplayNameCommand.CanExecute(null));
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);

        viewModel.SaveDisplayNameCommand.Execute(null);

        Assert.AreEqual("办公桌右屏", settingsService.Object.Settings.DockSettings.MonitorConfigs[0].DisplayNameOverride);
        Assert.AreEqual("办公桌右屏", viewModel.DisplayName);
        Assert.AreEqual("办公桌右屏", viewModel.DisplayNameInput);
        Assert.AreEqual("Wireless display", viewModel.DefaultDisplayName);
        Assert.IsTrue(changedProperties.Contains(nameof(viewModel.DisplayName)));
        Assert.IsFalse(viewModel.SaveDisplayNameCommand.CanExecute(null));
        Assert.IsTrue(viewModel.ResetDisplayNameCommand.CanExecute(null));
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    public void Save_PreservesMonitorIdentityAndOtherDockPreferences()
    {
        var original = Config() with
        {
            Enabled = false,
            Side = DockSide.Left,
            IsCustomized = true,
            CenterBands = ImmutableList.Create(new DockBandSettings { ProviderId = "datetime", CommandId = "clock" }),
        };
        var other = Config("Other screen") with { MonitorDeviceId = "other", FallbackDisplayNumber = 3 };
        var (viewModel, settingsService) = CreateEditor(original, otherConfigs: [other]);
        var originalSettings = settingsService.Object.Settings;

        viewModel.DisplayNameInput = "Desk screen";
        viewModel.SaveDisplayNameCommand.Execute(null);

        var actualSettings = settingsService.Object.Settings;
        Assert.AreEqual(original with { DisplayNameOverride = "Desk screen" }, actualSettings.DockSettings.MonitorConfigs[0]);
        Assert.AreSame(other, actualSettings.DockSettings.MonitorConfigs[1]);
        Assert.AreEqual(originalSettings, actualSettings with { DockSettings = originalSettings.DockSettings });
        Assert.AreEqual(originalSettings.DockSettings, actualSettings.DockSettings with { MonitorConfigs = originalSettings.DockSettings.MonitorConfigs });
        Assert.AreEqual(WirelessMonitor.DeviceId, viewModel.DeviceId);
        Assert.IsFalse(viewModel.IsEnabled, "A display can be renamed while its Dock is disabled.");
    }

    [TestMethod]
    public void RestoreDefault_ClearsSavedNameAndUnsavedDraft()
    {
        var (viewModel, settingsService) = CreateEditor(Config("Desk screen"));
        Assert.AreEqual("Desk screen", viewModel.DisplayNameInput);
        viewModel.DisplayNameInput = "Unsaved name";

        viewModel.ResetDisplayNameCommand.Execute(null);

        var config = settingsService.Object.Settings.DockSettings.MonitorConfigs[0];
        Assert.IsNull(config.DisplayNameOverride);
        Assert.AreEqual(2, config.FallbackDisplayNumber);
        Assert.AreEqual(string.Empty, viewModel.DisplayNameInput);
        Assert.AreEqual("Wireless display", viewModel.DisplayName);
        Assert.IsFalse(viewModel.SaveDisplayNameCommand.CanExecute(null));
        Assert.IsFalse(viewModel.ResetDisplayNameCommand.CanExecute(null));
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DataRow("", "Wireless display", "Wireless display")]
    [DataRow(" \t ", null, "Display B")]
    public void Save_BlankName_RestoresAutomaticName(string input, string? friendlyName, string expectedName)
    {
        var monitor = WirelessMonitor with { FriendlyName = friendlyName };
        var (viewModel, settingsService) = CreateEditor(Config("Desk screen"), monitor);

        viewModel.DisplayNameInput = input;
        viewModel.SaveDisplayNameCommand.Execute(null);

        Assert.IsNull(settingsService.Object.Settings.DockSettings.MonitorConfigs[0].DisplayNameOverride);
        Assert.AreEqual(string.Empty, viewModel.DisplayNameInput);
        Assert.AreEqual(expectedName, viewModel.DisplayName);
        Assert.AreEqual(expectedName, viewModel.DefaultDisplayName);
    }

    [TestMethod]
    public void Save_UnchangedName_DoesNotWriteSettingsAgain()
    {
        var (viewModel, settingsService) = CreateEditor(Config("Desk screen"));

        viewModel.DisplayNameInput = "  Desk screen  ";
        Assert.IsFalse(viewModel.SaveDisplayNameCommand.CanExecute(null));
        viewModel.SaveDisplayNameCommand.Execute(null);

        Assert.AreEqual("Desk screen", viewModel.DisplayNameInput);
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void RestoreDefault_UnsavedDraft_DoesNotWriteSettings()
    {
        var (viewModel, settingsService) = CreateEditor(Config());
        viewModel.DisplayNameInput = "Unsaved name";
        Assert.IsTrue(viewModel.ResetDisplayNameCommand.CanExecute(null));

        viewModel.ResetDisplayNameCommand.Execute(null);

        Assert.AreEqual(string.Empty, viewModel.DisplayNameInput);
        Assert.AreEqual("Wireless display", viewModel.DisplayName);
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void Override_SurvivesSettingsReloadAndReconnectWithNewDeviceAndFriendlyNames()
    {
        var primaryMonitor = WirelessMonitor with { StableId = "primary", DeviceId = @"\\.\DISPLAY1", IsPrimary = true };
        var primaryConfig = Config() with { MonitorDeviceId = primaryMonitor.StableId, IsPrimary = true, FallbackDisplayNumber = 1 };
        var (viewModel, settingsService) = CreateEditor(Config(), otherConfigs: [primaryConfig]);
        viewModel.DisplayNameInput = "客厅电视";
        viewModel.SaveDisplayNameCommand.Execute(null);

        var saved = settingsService.Object.Settings.DockSettings;
        var disconnected = MonitorConfigReconciler.Reconcile(saved.MonitorConfigs, [primaryMonitor], Now);
        var json = JsonSerializer.Serialize(saved with { MonitorConfigs = disconnected }, JsonSerializationContext.Default.DockSettings);
        var reloaded = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.DockSettings);
        Assert.IsNotNull(reloaded);

        var reconnectedMonitor = WirelessMonitor with
        {
            StableId = WirelessMonitor.StableId.ToLowerInvariant(),
            DeviceId = @"\\.\DISPLAY15",
            FriendlyName = "Updated hardware name",
        };
        var reconnected = MonitorConfigReconciler.Reconcile(reloaded.MonitorConfigs, [primaryMonitor, reconnectedMonitor], Now);
        var config = reconnected.Single(c => string.Equals(c.MonitorDeviceId, WirelessMonitor.StableId, StringComparison.OrdinalIgnoreCase));
        var (newViewModel, _) = CreateEditor(config, reconnectedMonitor);

        Assert.AreEqual(2, reconnected.Count);
        Assert.AreEqual("客厅电视", newViewModel.DisplayName);
        Assert.AreEqual("客厅电视", newViewModel.DisplayNameInput);
        Assert.AreEqual("Updated hardware name", newViewModel.DefaultDisplayName);
        Assert.AreEqual(@"\\.\DISPLAY15", newViewModel.DeviceId);
        Assert.AreEqual(2, config.FallbackDisplayNumber);
        Assert.AreEqual(WirelessMonitor.StableId, config.MonitorDeviceId);
    }

    [TestMethod]
    public void LegacySettingsWithoutOverride_StillUseAutomaticName()
    {
        var original = new DockSettings { MonitorConfigs = ImmutableList.Create(Config()) };
        var document = JsonNode.Parse(JsonSerializer.Serialize(original, JsonSerializationContext.Default.DockSettings))!;
        document["MonitorConfigs"]![0]!.AsObject().Remove("DisplayNameOverride");

        var settings = JsonSerializer.Deserialize(document.ToJsonString(), JsonSerializationContext.Default.DockSettings);
        Assert.IsNotNull(settings);
        var config = settings.MonitorConfigs[0];
        var (viewModel, _) = CreateEditor(config);

        Assert.IsNull(config.DisplayNameOverride);
        Assert.AreEqual("Wireless display", viewModel.DisplayName);
        Assert.AreEqual(string.Empty, viewModel.DisplayNameInput);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("Wireless display")]
    public void Resolve_CustomNameTakesPriorityAndPrimaryStatusRemainsDynamic(string? friendlyName)
    {
        var config = Config("Desk screen");
        var monitor = WirelessMonitor with { FriendlyName = friendlyName };

        Assert.AreEqual("Desk screen", DockMonitorDisplayName.Resolve(monitor, config));
        Assert.AreEqual("Desk screen (Primary)", DockMonitorDisplayName.Resolve(monitor with { IsPrimary = true }, config));
        Assert.AreEqual("Desk screen", config.DisplayNameOverride);
    }

    private static DockMonitorConfig Config(string? name = null) => new()
    {
        MonitorDeviceId = WirelessMonitor.StableId,
        FallbackDisplayNumber = 2,
        DisplayNameOverride = name,
        LastSeen = Now,
    };

    private static (DockMonitorConfigViewModel ViewModel, Mock<ISettingsService> SettingsService) CreateEditor(
        DockMonitorConfig config,
        MonitorInfo? monitor = null,
        params DockMonitorConfig[] otherConfigs)
    {
        var settings = new SettingsModel
        {
            DockSettings = new DockSettings { MonitorConfigs = ImmutableList.Create(config).AddRange(otherConfigs) },
        };
        var service = new Mock<ISettingsService>();
        service.Setup(s => s.Settings).Returns(() => settings);
        service.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, _) => settings = transform(settings));
        return (new DockMonitorConfigViewModel(config, monitor ?? WirelessMonitor, service.Object), service);
    }
}
