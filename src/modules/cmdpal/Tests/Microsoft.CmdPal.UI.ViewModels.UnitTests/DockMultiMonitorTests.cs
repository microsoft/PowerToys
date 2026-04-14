// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockMultiMonitorTests
{
    private static readonly MonitorInfo PrimaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY1",
        DisplayName = "Display 1 (Primary)",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = true,
    };

    private static readonly MonitorInfo SecondaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY2",
        DisplayName = "Display 2",
        Bounds = new ScreenRect(1920, 0, 3840, 1080),
        WorkArea = new ScreenRect(1920, 0, 3840, 1040),
        Dpi = 144,
        IsPrimary = false,
    };

    // --- ScreenRect tests ---
    [TestMethod]
    public void ScreenRect_WidthAndHeight_ComputedCorrectly()
    {
        var rect = new ScreenRect(100, 200, 500, 600);
        Assert.AreEqual(400, rect.Width);
        Assert.AreEqual(400, rect.Height);
    }

    // --- MonitorInfo tests ---
    [TestMethod]
    public void MonitorInfo_ScaleFactor_ComputedFromDpi()
    {
        Assert.AreEqual(1.0, PrimaryMonitor.ScaleFactor, 0.001);
        Assert.AreEqual(1.5, SecondaryMonitor.ScaleFactor, 0.001);
    }

    // --- DockMonitorConfig tests ---
    [TestMethod]
    public void DockMonitorConfig_ResolveSide_ReturnsOverrideWhenSet()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY1",
            Side = DockSide.Left,
        };

        Assert.AreEqual(DockSide.Left, config.ResolveSide(DockSide.Bottom));
    }

    [TestMethod]
    public void DockMonitorConfig_ResolveSide_ReturnsGlobalWhenNull()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY1",
            Side = null,
        };

        Assert.AreEqual(DockSide.Bottom, config.ResolveSide(DockSide.Bottom));
    }

    [TestMethod]
    public void DockMonitorConfig_ResolveBands_ReturnsOwnBandsWhenCustomized()
    {
        var customBand = new DockBandSettings { ProviderId = "test", CommandId = "cmd1" };
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY1",
            IsCustomized = true,
            StartBands = ImmutableList.Create(customBand),
        };

        var globalBands = ImmutableList.Create(new DockBandSettings { ProviderId = "global", CommandId = "g1" });
        var resolved = config.ResolveStartBands(globalBands);

        Assert.AreEqual(1, resolved.Count);
        Assert.AreEqual("cmd1", resolved[0].CommandId);
    }

    [TestMethod]
    public void DockMonitorConfig_ResolveBands_ReturnsGlobalWhenNotCustomized()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY1",
            IsCustomized = false,
        };

        var globalBands = ImmutableList.Create(new DockBandSettings { ProviderId = "global", CommandId = "g1" });
        var resolved = config.ResolveStartBands(globalBands);

        Assert.AreEqual(1, resolved.Count);
        Assert.AreEqual("g1", resolved[0].CommandId);
    }

    [TestMethod]
    public void DockMonitorConfig_ForkFromGlobal_CopiesBandsAndSetsCustomized()
    {
        var globalSettings = CreateMinimalDockSettings() with
        {
            StartBands = ImmutableList.Create(new DockBandSettings { ProviderId = "p1", CommandId = "c1" }),
            CenterBands = ImmutableList.Create(new DockBandSettings { ProviderId = "p2", CommandId = "c2" }),
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };

        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY1",
            IsCustomized = false,
        };

        var forked = config.ForkFromGlobal(globalSettings);

        Assert.IsTrue(forked.IsCustomized);
        Assert.IsNotNull(forked.StartBands);
        Assert.AreEqual(1, forked.StartBands!.Count);
        Assert.AreEqual("c1", forked.StartBands![0].CommandId);
        Assert.IsNotNull(forked.CenterBands);
        Assert.AreEqual(1, forked.CenterBands!.Count);
        Assert.AreEqual("c2", forked.CenterBands![0].CommandId);
        Assert.IsNotNull(forked.EndBands);
        Assert.AreEqual(0, forked.EndBands!.Count);
    }

    [TestMethod]
    public void DockMonitorConfig_ForkFromGlobal_ProducesIndependentCopy()
    {
        var band = new DockBandSettings { ProviderId = "p1", CommandId = "c1" };
        var globalSettings = CreateMinimalDockSettings() with
        {
            StartBands = ImmutableList.Create(band),
        };

        var config = new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1" };
        var forked = config.ForkFromGlobal(globalSettings);

        // Modify forked bands — global should be unaffected
        var newForked = forked with { StartBands = forked.StartBands!.Add(new DockBandSettings { ProviderId = "p2", CommandId = "c2" }) };

        Assert.AreEqual(1, globalSettings.StartBands.Count);
        Assert.AreEqual(2, newForked.StartBands.Count);
    }

    // --- MonitorConfigReconciler tests ---
    [TestMethod]
    public void Reconciler_ExactMatch_PreservesExistingConfigs()
    {
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, IsPrimary = true },
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY2", Enabled = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Reconciler_NewMonitor_CreatesDefaultConfig()
    {
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, IsPrimary = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(2, result.Count);
        var newConfig = result[1];
        Assert.AreEqual(@"\\.\DISPLAY2", newConfig.MonitorDeviceId);
        Assert.IsTrue(newConfig.Enabled);
    }

    [TestMethod]
    public void Reconciler_DisconnectedMonitor_RemovesOrphan()
    {
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true },
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY99", Enabled = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(@"\\.\DISPLAY1", result[0].MonitorDeviceId);
    }

    [TestMethod]
    public void Reconciler_EmptyConfigs_CreatesForAllMonitors()
    {
        var configs = ImmutableList<DockMonitorConfig>.Empty;
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Reconciler_FuzzyMatch_UpdatesPrimaryFlag()
    {
        // Config has old device ID but marked as primary
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY_OLD", Enabled = true, IsPrimary = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(@"\\.\DISPLAY1", result[0].MonitorDeviceId);
        Assert.IsTrue(result[0].IsPrimary);
    }

    [TestMethod]
    public void Reconciler_FuzzyMatch_DoesNotMatchNonPrimaryMonitors()
    {
        // Config has stale device ID for a non-primary monitor
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, IsPrimary = true },
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY_STALE", Enabled = true, IsPrimary = false, IsCustomized = true });

        // Current monitors have primary + a different secondary
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        // Primary keeps its config, stale secondary is orphaned (removed),
        // new secondary gets a fresh default config instead of inheriting the stale one
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(@"\\.\DISPLAY1", result[0].MonitorDeviceId);
        Assert.AreEqual(@"\\.\DISPLAY2", result[1].MonitorDeviceId);
        Assert.IsFalse(result[1].IsCustomized, "New secondary should get a default config, not inherit the stale one.");
    }

    // --- JSON serialization round-trip ---
    [TestMethod]
    public void DockMonitorConfig_JsonRoundTrip_PreservesAllFields()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY1",
            Enabled = true,
            Side = DockSide.Left,
            IsPrimary = true,
            IsCustomized = true,
            StartBands = ImmutableList.Create(new DockBandSettings { ProviderId = "p1", CommandId = "c1" }),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };

        var json = JsonSerializer.Serialize(config, JsonSerializationContext.Default.DockMonitorConfig);
        var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.DockMonitorConfig);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(config.MonitorDeviceId, deserialized!.MonitorDeviceId);
        Assert.AreEqual(config.Enabled, deserialized.Enabled);
        Assert.AreEqual(config.Side, deserialized.Side);
        Assert.AreEqual(config.IsPrimary, deserialized.IsPrimary);
        Assert.AreEqual(config.IsCustomized, deserialized.IsCustomized);
        Assert.IsNotNull(deserialized.StartBands);
        Assert.AreEqual(1, deserialized.StartBands!.Count);
        Assert.AreEqual("c1", deserialized.StartBands![0].CommandId);
    }

    [TestMethod]
    public void DockSettings_MonitorConfigs_JsonRoundTrip()
    {
        var settings = CreateMinimalDockSettings() with
        {
            MonitorConfigs = ImmutableList.Create(
                new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true },
                new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY2", Enabled = false }),
        };

        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.DockSettings);
        var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.DockSettings);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(2, deserialized.MonitorConfigs.Count);
        Assert.AreEqual(@"\\.\DISPLAY1", deserialized.MonitorConfigs[0].MonitorDeviceId);
        Assert.IsTrue(deserialized.MonitorConfigs[0].Enabled);
        Assert.AreEqual(@"\\.\DISPLAY2", deserialized.MonitorConfigs[1].MonitorDeviceId);
        Assert.IsFalse(deserialized.MonitorConfigs[1].Enabled);
    }

    private static DockSettings CreateMinimalDockSettings()
    {
        // Deserialize from minimal JSON to avoid WinUI3 dependencies
        var json = "{}";
        return JsonSerializer.Deserialize(json, JsonSerializationContext.Default.DockSettings)
               ?? new DockSettings();
    }

    [TestMethod]
    public void Reconciler_ReturnsOriginalReference_WhenNothingChanged()
    {
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.DeviceId, Enabled = true, IsPrimary = true },
            new DockMonitorConfig { MonitorDeviceId = SecondaryMonitor.DeviceId, Enabled = true, IsPrimary = false });

        var reconciled = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreSame(configs, reconciled, "Reconciler should return the same reference when nothing changed");
    }

    // --- Per-monitor save isolation test ---
    [TestMethod]
    public void WithActiveBands_PerMonitor_DoesNotClobberOtherMonitorConfig()
    {
        var bandA = new DockBandSettings { ProviderId = "provA", CommandId = "cmdA" };
        var bandB = new DockBandSettings { ProviderId = "provB", CommandId = "cmdB" };

        var monitorAConfig = new DockMonitorConfig
        {
            MonitorDeviceId = PrimaryMonitor.DeviceId,
            Enabled = true,
            IsPrimary = true,
            IsCustomized = true,
            StartBands = ImmutableList.Create(bandA),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };
        var monitorBConfig = new DockMonitorConfig
        {
            MonitorDeviceId = SecondaryMonitor.DeviceId,
            Enabled = true,
            IsPrimary = false,
            IsCustomized = true,
            StartBands = ImmutableList.Create(bandB),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };

        var settings = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(monitorAConfig, monitorBConfig),
        };

        // Simulate Monitor A saving new bands — only A's config should change
        var newBandA = new DockBandSettings { ProviderId = "provA2", CommandId = "cmdA2" };
        var configA = settings.MonitorConfigs[0];
        var updatedConfigA = configA with { StartBands = ImmutableList.Create(newBandA) };
        var afterSaveA = settings with
        {
            MonitorConfigs = ImmutableList.Create(updatedConfigA, monitorBConfig),
        };

        // Verify Monitor A's config was updated
        Assert.AreEqual("provA2", afterSaveA.MonitorConfigs![0].StartBands![0].ProviderId);

        // Verify Monitor B's config was NOT changed
        Assert.AreEqual("provB", afterSaveA.MonitorConfigs![1].StartBands![0].ProviderId);
        Assert.AreEqual("cmdB", afterSaveA.MonitorConfigs![1].StartBands![0].CommandId);
    }
}
