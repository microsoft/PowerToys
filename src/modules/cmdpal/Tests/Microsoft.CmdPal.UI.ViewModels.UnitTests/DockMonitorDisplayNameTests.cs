// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockMonitorDisplayNameTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);

    private static readonly MonitorInfo WirelessMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY14",
        StableId = @"\\?\DISPLAY#WIRELESS#UID256#{guid}",
        DisplayName = "Display 14",
        Bounds = new ScreenRect(1920, 0, 3840, 1080),
        WorkArea = new ScreenRect(1920, 0, 3840, 1040),
        Dpi = 96,
        IsPrimary = false,
    };

    [TestMethod]
    public void Reconcile_ReconnectAfterSettingsReload_PreservesNameAndUpdatesRuntimeId()
    {
        var connected = MonitorConfigReconciler.Reconcile(null, [WirelessMonitor], Now);
        var originalName = CreateViewModel(connected[0], WirelessMonitor).DisplayName;
        var disconnected = MonitorConfigReconciler.Reconcile(connected, [], Now);
        var settings = new DockSettings { MonitorConfigs = disconnected };
        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.DockSettings);
        var reloaded = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.DockSettings);
        Assert.IsNotNull(reloaded);

        var reconnectedMonitor = WirelessMonitor with
        {
            DeviceId = @"\\.\DISPLAY15",
            DisplayName = "Display 15",
            StableId = WirelessMonitor.StableId.ToLowerInvariant(),
        };
        var reconnected = MonitorConfigReconciler.Reconcile(reloaded.MonitorConfigs, [reconnectedMonitor], Now);
        var viewModel = CreateViewModel(reconnected[0], reconnectedMonitor);

        Assert.AreEqual(1, reconnected.Count);
        Assert.AreEqual("Display A", originalName);
        Assert.AreEqual(originalName, viewModel.DisplayName);
        Assert.AreEqual(@"\\.\DISPLAY15", viewModel.DeviceId);
        Assert.AreSame(reloaded.MonitorConfigs, reconnected, "Reconnecting must not rewrite an unchanged name or configuration.");
    }

    [TestMethod]
    public void Reconcile_LegacyConfig_BackfillsNameOnceAndPreservesCustomization()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = WirelessMonitor.DeviceId,
            Enabled = true,
            Side = DockSide.Left,
            IsCustomized = true,
            CenterBands = ImmutableList.Create(new DockBandSettings { ProviderId = "datetime", CommandId = "clock" }),
        };
        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(config), [WirelessMonitor], Now);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(WirelessMonitor.StableId, result[0].MonitorDeviceId);
        Assert.AreEqual("Display A", CreateViewModel(result[0], WirelessMonitor).DisplayName);
        Assert.IsTrue(result[0].Enabled);
        Assert.IsTrue(result[0].IsCustomized);
        Assert.AreEqual(DockSide.Left, result[0].Side);
        Assert.AreEqual(config.CenterBands, result[0].CenterBands);
        Assert.AreSame(result, MonitorConfigReconciler.Reconcile(result, [WirelessMonitor], Now));
    }

    [TestMethod]
    public void Reconcile_ReorderedMonitors_KeepAssignedNames()
    {
        var otherMonitor = WirelessMonitor with { StableId = "other", DeviceId = @"\\.\DISPLAY2" };
        var initial = MonitorConfigReconciler.Reconcile(null, [WirelessMonitor, otherMonitor], Now);
        var reordered = MonitorConfigReconciler.Reconcile(initial, [otherMonitor, WirelessMonitor], Now);

        Assert.AreEqual(2, reordered.Count);
        foreach (var config in initial)
        {
            var current = reordered.Single(c => c.MonitorDeviceId == config.MonitorDeviceId);
            Assert.AreEqual(config.FallbackDisplayNumber, current.FallbackDisplayNumber);
        }

        Assert.AreEqual(2, reordered.Select(c => c.FallbackDisplayNumber).Distinct().Count());
    }

    [TestMethod]
    public void Reconcile_DisconnectedMonitor_KeepsLabelReservedWhenAnotherConnects()
    {
        var primaryMonitor = WirelessMonitor with { StableId = "primary", DeviceId = @"\\.\DISPLAY1", IsPrimary = true };
        var newMonitor = WirelessMonitor with { StableId = "new", DeviceId = @"\\.\DISPLAY3" };
        var initial = MonitorConfigReconciler.Reconcile(null, [primaryMonitor, WirelessMonitor], Now);
        var replaced = MonitorConfigReconciler.Reconcile(initial, [primaryMonitor, newMonitor], Now);

        Assert.AreEqual(3, replaced.Count, "The disconnected monitor must retain its configuration and name.");
        var wirelessConfig = replaced.Single(c => c.MonitorDeviceId == WirelessMonitor.StableId);
        var newConfig = replaced.Single(c => c.MonitorDeviceId == newMonitor.StableId);
        Assert.AreEqual("Display B", DockMonitorDisplayName.Resolve(WirelessMonitor, wirelessConfig));
        Assert.AreEqual("Display C", DockMonitorDisplayName.Resolve(newMonitor, newConfig));

        var reconnected = MonitorConfigReconciler.Reconcile(replaced, [WirelessMonitor, newMonitor, primaryMonitor], Now);
        Assert.AreEqual(wirelessConfig.FallbackDisplayNumber, reconnected.Single(c => c.MonitorDeviceId == WirelessMonitor.StableId).FallbackDisplayNumber);
        Assert.AreEqual(newConfig.FallbackDisplayNumber, reconnected.Single(c => c.MonitorDeviceId == newMonitor.StableId).FallbackDisplayNumber);
    }

    [TestMethod]
    public void Reconcile_MissingName_DoesNotTakeAnExistingNameLaterInTheList()
    {
        var otherMonitor = WirelessMonitor with { StableId = "other", DeviceId = @"\\.\DISPLAY2" };
        var existing = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = WirelessMonitor.StableId },
            new DockMonitorConfig { MonitorDeviceId = otherMonitor.StableId, FallbackDisplayNumber = 1 });

        var result = MonitorConfigReconciler.Reconcile(existing, [WirelessMonitor, otherMonitor], Now);

        Assert.AreEqual(2, result[0].FallbackDisplayNumber);
        Assert.AreEqual(1, result[1].FallbackDisplayNumber);
        Assert.AreSame(result, MonitorConfigReconciler.Reconcile(result, [WirelessMonitor, otherMonitor], Now));
    }

    [TestMethod]
    public void Reconcile_FriendlyNameUnavailable_KeepsReservedFallbackName()
    {
        var namedMonitor = WirelessMonitor with { FriendlyName = "DELL U2723QE" };
        var initial = MonitorConfigReconciler.Reconcile(null, [namedMonitor], Now);
        Assert.AreEqual("DELL U2723QE", CreateViewModel(initial[0], namedMonitor).DisplayName);

        var unnamed = MonitorConfigReconciler.Reconcile(initial, [WirelessMonitor], Now);
        Assert.AreSame(initial, unnamed);
        Assert.AreEqual("Display A", CreateViewModel(unnamed[0], WirelessMonitor).DisplayName);

        var namedAgain = MonitorConfigReconciler.Reconcile(unnamed, [namedMonitor], Now);
        Assert.AreEqual("DELL U2723QE", CreateViewModel(namedAgain[0], namedMonitor).DisplayName);
        Assert.AreEqual(initial[0].FallbackDisplayNumber, namedAgain[0].FallbackDisplayNumber);
    }

    [TestMethod]
    public void Resolve_PrimaryDisplayChanges_UpdatesSuffixWithoutChangingIdentity()
    {
        var initial = MonitorConfigReconciler.Reconcile(null, [WirelessMonitor], Now);
        var primaryMonitor = WirelessMonitor with { IsPrimary = true };
        var primary = MonitorConfigReconciler.Reconcile(initial, [primaryMonitor], Now);
        var secondary = MonitorConfigReconciler.Reconcile(primary, [WirelessMonitor], Now);

        Assert.AreEqual("Display A (Primary)", CreateViewModel(primary[0], primaryMonitor).DisplayName);
        Assert.AreEqual("Display A", CreateViewModel(secondary[0], WirelessMonitor).DisplayName);
        Assert.AreEqual(initial[0].FallbackDisplayNumber, primary[0].FallbackDisplayNumber);
        Assert.AreEqual(initial[0].FallbackDisplayNumber, secondary[0].FallbackDisplayNumber);
    }

    [TestMethod]
    [DataRow(1, "A")]
    [DataRow(26, "Z")]
    [DataRow(27, "AA")]
    [DataRow(52, "AZ")]
    [DataRow(53, "BA")]
    public void Resolve_PersistedOrdinal_FormatsLetterLabel(int number, string label)
    {
        var config = new DockMonitorConfig { MonitorDeviceId = WirelessMonitor.StableId, FallbackDisplayNumber = number };

        Assert.AreEqual($"Display {label}", DockMonitorDisplayName.Resolve(WirelessMonitor, config));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Resolve_MissingFriendlyName_UsesPersistedLabel(string? friendlyName)
    {
        var monitor = WirelessMonitor with { FriendlyName = friendlyName };
        var config = new DockMonitorConfig { MonitorDeviceId = monitor.StableId, FallbackDisplayNumber = 2 };

        Assert.AreEqual("Display B", DockMonitorDisplayName.Resolve(monitor, config));
    }

    [TestMethod]
    public void Resolve_NoConfig_DoesNotExposeVolatileGdiName()
    {
        Assert.AreEqual("Display", DockMonitorDisplayName.Resolve(WirelessMonitor, null));
        Assert.AreEqual("DELL U2723QE", DockMonitorDisplayName.Resolve(WirelessMonitor with { FriendlyName = "DELL U2723QE" }, null));
    }

    private static DockMonitorConfigViewModel CreateViewModel(DockMonitorConfig config, MonitorInfo monitor)
    {
        var settings = new SettingsModel
        {
            DockSettings = new DockSettings { MonitorConfigs = ImmutableList.Create(config) },
        };
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(settings);
        return new DockMonitorConfigViewModel(config, monitor, settingsService.Object);
    }
}
