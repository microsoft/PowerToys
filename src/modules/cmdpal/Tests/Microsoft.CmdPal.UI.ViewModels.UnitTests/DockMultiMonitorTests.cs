// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockMultiMonitorTests
{
    private static readonly MonitorInfo PrimaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY1",
        StableId = @"\\?\DISPLAY#PRI1234#4&aaa&0&UID111#{guid1}",
        DisplayName = "Display 1 (Primary)",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = true,
    };

    private static readonly MonitorInfo SecondaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY2",
        StableId = @"\\?\DISPLAY#SEC5678#4&bbb&0&UID222#{guid2}",
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
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true },
            new DockMonitorConfig { MonitorDeviceId = SecondaryMonitor.StableId, Enabled = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Reconciler_NewMonitor_CreatesDefaultConfig()
    {
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(2, result.Count);
        var newConfig = result[1];
        Assert.AreEqual(SecondaryMonitor.StableId, newConfig.MonitorDeviceId);
        Assert.IsTrue(newConfig.Enabled);
    }

    [TestMethod]
    public void Reconciler_DisconnectedMonitor_PreservesConfig()
    {
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, LastSeen = DateTime.UtcNow },
            new DockMonitorConfig { MonitorDeviceId = @"\\?\DISPLAY#GONE#4&ccc&0&UID999#{guid99}", Enabled = true, IsCustomized = true, LastSeen = DateTime.UtcNow });

        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(PrimaryMonitor.StableId, result[0].MonitorDeviceId);
        Assert.AreEqual(@"\\?\DISPLAY#GONE#4&ccc&0&UID999#{guid99}", result[1].MonitorDeviceId);
        Assert.IsTrue(result[1].IsCustomized, "Disconnected monitor should preserve its customizations.");
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
    public void Reconciler_NewPrimaryMonitor_InheritsGlobalBands()
    {
        // On first run / upgrade, the primary monitor should inherit global bands
        var configs = ImmutableList<DockMonitorConfig>.Empty;
        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(1, result.Count);
        Assert.IsFalse(result[0].IsCustomized, "Primary monitor should inherit global bands (IsCustomized = false).");
    }

    [TestMethod]
    public void Reconciler_NewSecondaryMonitor_StartsWithEmptyBands()
    {
        // On first run / upgrade with multi-monitor, secondary monitors should start
        // with empty bands so users are not forced to manually unpin from every display.
        var configs = ImmutableList<DockMonitorConfig>.Empty;
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        var secondary = result.Find(c => !c.IsPrimary);
        Assert.IsNotNull(secondary, "A secondary monitor config should have been created.");
        Assert.IsTrue(secondary!.IsCustomized, "Secondary monitor should be customized (IsCustomized = true).");
        Assert.AreEqual(0, secondary.StartBands?.Count ?? 0, "Secondary monitor should start with empty StartBands.");
        Assert.AreEqual(0, secondary.CenterBands?.Count ?? 0, "Secondary monitor should start with empty CenterBands.");
        Assert.AreEqual(0, secondary.EndBands?.Count ?? 0, "Secondary monitor should start with empty EndBands.");
    }

    [TestMethod]
    public void Reconciler_FuzzyMatch_UpdatesPrimaryFlag()
    {
        // Config has old stable ID but marked as primary
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\?\DISPLAY#OLD#4&ddd&0&UID000#{guidOld}", Enabled = true, IsPrimary = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(PrimaryMonitor.StableId, result[0].MonitorDeviceId);
        Assert.IsTrue(result[0].IsPrimary);
    }

    [TestMethod]
    public void Reconciler_FuzzyMatch_DoesNotMatchNonPrimaryMonitors()
    {
        // Config has stale stable ID for a non-primary monitor
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true },
            new DockMonitorConfig { MonitorDeviceId = @"\\?\DISPLAY#STALE#4&eee&0&UID333#{guidStale}", Enabled = true, IsPrimary = false, IsCustomized = true, LastSeen = DateTime.UtcNow });

        // Current monitors have primary + a different secondary
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        // Primary keeps its config, new secondary gets a fresh customized config,
        // stale secondary is retained at end for future reconnection
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(PrimaryMonitor.StableId, result[0].MonitorDeviceId);
        Assert.AreEqual(SecondaryMonitor.StableId, result[1].MonitorDeviceId);
        Assert.IsTrue(result[1].IsCustomized, "New secondary should get an empty-bands customized config.");
        Assert.AreEqual(0, result[1].StartBands?.Count ?? 0, "New secondary should start with empty bands.");
        Assert.AreEqual(@"\\?\DISPLAY#STALE#4&eee&0&UID333#{guidStale}", result[2].MonitorDeviceId, "Stale config should be preserved.");
        Assert.IsTrue(result[2].IsCustomized, "Stale config should retain its customizations.");
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
        var now = DateTime.UtcNow;
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true, LastSeen = now },
            new DockMonitorConfig { MonitorDeviceId = SecondaryMonitor.StableId, Enabled = true, IsPrimary = false, LastSeen = now });

        var reconciled = MonitorConfigReconciler.Reconcile(configs, monitors, now);

        Assert.AreSame(configs, reconciled, "Reconciler should return the same reference when nothing changed");
    }

    // --- Per-monitor save isolation test ---
    [TestMethod]
    public void WithActiveBands_PerMonitor_DoesNotClobberOtherMonitorConfig()
    {
        var bandA = new DockBandSettings { ProviderId = "provA", CommandId = "cmdA" };
        var bandB = new DockBandSettings { ProviderId = "provB", CommandId = "cmdB" };

        var monitorOneConfig = new DockMonitorConfig
        {
            MonitorDeviceId = PrimaryMonitor.StableId,
            Enabled = true,
            IsPrimary = true,
            IsCustomized = true,
            StartBands = ImmutableList.Create(bandA),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };
        var monitorTwoConfig = new DockMonitorConfig
        {
            MonitorDeviceId = SecondaryMonitor.StableId,
            Enabled = true,
            IsPrimary = false,
            IsCustomized = true,
            StartBands = ImmutableList.Create(bandB),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
        };

        var settings = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(monitorOneConfig, monitorTwoConfig),
        };

        // Simulate Monitor A saving new bands — only A's config should change
        var newBandA = new DockBandSettings { ProviderId = "provA2", CommandId = "cmdA2" };
        var configA = settings.MonitorConfigs[0];
        var updatedConfigA = configA with { StartBands = ImmutableList.Create(newBandA) };
        var afterSaveA = settings with
        {
            MonitorConfigs = ImmutableList.Create(updatedConfigA, monitorTwoConfig),
        };

        // Verify Monitor A's config was updated
        Assert.AreEqual("provA2", afterSaveA.MonitorConfigs![0].StartBands![0].ProviderId);

        // Verify Monitor B's config was NOT changed
        Assert.AreEqual("provB", afterSaveA.MonitorConfigs![1].StartBands![0].ProviderId);
        Assert.AreEqual("cmdB", afterSaveA.MonitorConfigs![1].StartBands![0].CommandId);
    }

    [TestMethod]
    public void AllPinnedCommands_IncludesPerMonitorBands()
    {
        // Set up global bands
        var globalBand = new DockBandSettings { ProviderId = "prov1", CommandId = "globalCmd" };

        // Set up a customized per-monitor config with a unique band
        var perMonitorBand = new DockBandSettings { ProviderId = "prov1", CommandId = "monitorOnlyCmd" };
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY2",
            IsCustomized = true,
            StartBands = ImmutableList.Create(globalBand, perMonitorBand),
        };

        var settings = new DockSettings
        {
            StartBands = ImmutableList.Create(globalBand),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
            MonitorConfigs = ImmutableList.Create(config),
        };

        var allPinned = new List<(string ProviderId, string CommandId)>(settings.AllPinnedCommands);

        // Should include the global band AND the per-monitor band
        Assert.IsTrue(allPinned.Exists(p => p.CommandId == "globalCmd"), "Global band should be included");
        Assert.IsTrue(allPinned.Exists(p => p.CommandId == "monitorOnlyCmd"), "Per-monitor band should be included");
    }

    [TestMethod]
    public void AllPinnedCommands_ExcludesNonCustomizedMonitorBands()
    {
        var globalBand = new DockBandSettings { ProviderId = "prov1", CommandId = "globalCmd" };

        // Non-customized config should NOT contribute its own bands
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = @"\\.\DISPLAY2",
            IsCustomized = false,
            StartBands = ImmutableList.Create(new DockBandSettings { ProviderId = "prov1", CommandId = "shouldNotAppear" }),
        };

        var settings = new DockSettings
        {
            StartBands = ImmutableList.Create(globalBand),
            CenterBands = ImmutableList<DockBandSettings>.Empty,
            EndBands = ImmutableList<DockBandSettings>.Empty,
            MonitorConfigs = ImmutableList.Create(config),
        };

        var allPinned = new List<(string ProviderId, string CommandId)>(settings.AllPinnedCommands);

        Assert.IsTrue(allPinned.Exists(p => p.CommandId == "globalCmd"), "Global band should be included");
        Assert.IsFalse(allPinned.Exists(p => p.CommandId == "shouldNotAppear"), "Non-customized per-monitor band should NOT be included");
    }

    // --- DockMonitorConfigViewModel tests ---
    [TestMethod]
    public void DockMonitorConfigViewModel_IsEnabled_ReadsFromConfig()
    {
        var settings = CreateSettingsModelWithConfigs(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = false, IsPrimary = true });

        var mockSettings = CreateMockSettingsService(settings);
        var vm = new DockMonitorConfigViewModel(
            settings.DockSettings.MonitorConfigs[0], PrimaryMonitor, mockSettings.Object);

        Assert.IsFalse(vm.IsEnabled);
    }

    [TestMethod]
    public void DockMonitorConfigViewModel_IsEnabled_PersistsChange()
    {
        var settings = CreateSettingsModelWithConfigs(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true });

        var mockSettings = CreateMockSettingsService(settings);
        var vm = new DockMonitorConfigViewModel(
            settings.DockSettings.MonitorConfigs[0], PrimaryMonitor, mockSettings.Object);

        vm.IsEnabled = false;

        mockSettings.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    public void DockMonitorConfigViewModel_SideOverrideIndex_ReturnsZeroWhenNull()
    {
        var settings = CreateSettingsModelWithConfigs(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Side = null });

        var mockSettings = CreateMockSettingsService(settings);
        var vm = new DockMonitorConfigViewModel(
            settings.DockSettings.MonitorConfigs[0], PrimaryMonitor, mockSettings.Object);

        Assert.AreEqual(0, vm.SideOverrideIndex);
        Assert.IsFalse(vm.HasSideOverride);
    }

    [TestMethod]
    public void DockMonitorConfigViewModel_SideOverrideIndex_MapsCorrectly()
    {
        var settings = CreateSettingsModelWithConfigs(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Side = DockSide.Right });

        var mockSettings = CreateMockSettingsService(settings);
        var vm = new DockMonitorConfigViewModel(
            settings.DockSettings.MonitorConfigs[0], PrimaryMonitor, mockSettings.Object);

        Assert.AreEqual(3, vm.SideOverrideIndex);
        Assert.IsTrue(vm.HasSideOverride);
    }

    [TestMethod]
    public void DockMonitorConfigViewModel_DisplayInfo_ExposesMonitorProperties()
    {
        var settings = CreateSettingsModelWithConfigs(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, IsPrimary = true });

        var mockSettings = CreateMockSettingsService(settings);
        var vm = new DockMonitorConfigViewModel(
            settings.DockSettings.MonitorConfigs[0], PrimaryMonitor, mockSettings.Object);

        Assert.AreEqual("Display 1 (Primary)", vm.DisplayName);
        Assert.AreEqual(PrimaryMonitor.DeviceId, vm.DeviceId);
        Assert.IsTrue(vm.IsPrimary);
        Assert.AreEqual("1920 \u00D7 1080", vm.Resolution);
    }

    [TestMethod]
    public void Reconciler_EmptyConfigs_CreatesDefaultsForAllMonitors()
    {
        // Simulate upgrade: no per-monitor configs, 3 monitors connected
        var tertiary = new MonitorInfo
        {
            DeviceId = @"\\.\DISPLAY3",
            StableId = @"\\?\DISPLAY#TER9012#4&fff&0&UID333#{guid3}",
            DisplayName = "Display 3",
            Bounds = new ScreenRect(3840, 0, 5760, 1080),
            WorkArea = new ScreenRect(3840, 0, 5760, 1040),
            Dpi = 96,
            IsPrimary = false,
        };
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor, tertiary };
        var emptyConfigs = ImmutableList<DockMonitorConfig>.Empty;

        var reconciled = MonitorConfigReconciler.Reconcile(emptyConfigs, monitors);

        Assert.AreEqual(3, reconciled.Count, "Should create configs for all 3 monitors");

        // All monitors should be enabled by default
        foreach (var config in reconciled)
        {
            Assert.IsTrue(config.Enabled, $"Monitor {config.MonitorDeviceId} should be enabled");
            Assert.IsNull(config.Side, $"Monitor {config.MonitorDeviceId} should inherit global side");
        }

        // Primary inherits global bands (IsCustomized=false); secondary starts with
        // empty bands (IsCustomized=true) so users choose what to pin per-monitor.
        var primaryCfg = reconciled.Find(c => c.IsPrimary);
        Assert.IsFalse(primaryCfg!.IsCustomized, "Primary should inherit global bands");
        foreach (var config in reconciled)
        {
            if (!config.IsPrimary)
            {
                Assert.IsTrue(config.IsCustomized, $"Monitor {config.MonitorDeviceId} (secondary) should be customized with empty bands");
            }
        }

        // Primary should be flagged correctly
        var primaryConfig = reconciled.Find(c => c.MonitorDeviceId == PrimaryMonitor.StableId);
        Assert.IsNotNull(primaryConfig, "Primary monitor config should exist");
        Assert.IsTrue(primaryConfig.IsPrimary, "Primary config should be marked as primary");

        var secondaryConfig = reconciled.Find(c => c.MonitorDeviceId == SecondaryMonitor.StableId);
        Assert.IsNotNull(secondaryConfig, "Secondary monitor config should exist");
        Assert.IsFalse(secondaryConfig.IsPrimary, "Secondary config should not be marked as primary");
    }

    [TestMethod]
    public void Reconcile_NullExistingConfigs_CreatesDefaultsForAllMonitors()
    {
        // Simulate upgrade from a version that didn't have multi-monitor settings —
        // MonitorConfigs is null because it didn't exist in the older settings.json.
        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var reconciled = MonitorConfigReconciler.Reconcile(null, monitors);

        Assert.AreEqual(2, reconciled.Count, "Should create configs for all monitors even when existing configs is null");

        var primaryConfig = reconciled.Find(c => c.IsPrimary);
        Assert.IsNotNull(primaryConfig, "Primary config should be created");
        Assert.IsTrue(primaryConfig.Enabled, "Primary should be enabled by default");
        Assert.IsFalse(primaryConfig.IsCustomized, "Primary should inherit global bands");

        var secondaryConfig = reconciled.Find(c => !c.IsPrimary);
        Assert.IsNotNull(secondaryConfig, "Secondary config should be created");
        Assert.IsTrue(secondaryConfig.Enabled, "Secondary should be enabled by default");
        Assert.IsTrue(secondaryConfig.IsCustomized, "Secondary should start with custom (empty) bands");
    }

    [TestMethod]
    public void Reconcile_DisconnectThenReconnect_PreservesCustomizations()
    {
        // Step 1: Both monitors connected with customized secondary
        var customBands = ImmutableList.Create(new DockBandSettings { ProviderId = "custom", CommandId = "cmd1" });
        var now = DateTime.UtcNow;
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true, LastSeen = now },
            new DockMonitorConfig
            {
                MonitorDeviceId = SecondaryMonitor.StableId,
                Enabled = true,
                IsPrimary = false,
                IsCustomized = true,
                StartBands = customBands,
                CenterBands = ImmutableList<DockBandSettings>.Empty,
                EndBands = ImmutableList<DockBandSettings>.Empty,
                Side = DockSide.Left,
                LastSeen = now,
            });

        // Step 2: Disconnect secondary monitor
        var onlyPrimary = new List<MonitorInfo> { PrimaryMonitor };
        var afterDisconnect = MonitorConfigReconciler.Reconcile(configs, onlyPrimary, now);

        Assert.AreEqual(2, afterDisconnect.Count, "Disconnected monitor config should be retained");

        // Step 3: Reconnect secondary monitor
        var bothMonitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };
        var afterReconnect = MonitorConfigReconciler.Reconcile(afterDisconnect, bothMonitors, now);

        // Verify customizations survived the round-trip
        var secondaryConfig = afterReconnect.Find(c =>
            string.Equals(c.MonitorDeviceId, SecondaryMonitor.StableId, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(secondaryConfig, "Secondary config should be found after reconnection");
        Assert.IsTrue(secondaryConfig.IsCustomized, "Customization flag should survive");
        Assert.AreEqual(DockSide.Left, secondaryConfig.Side, "Side override should survive");
        Assert.AreEqual(1, secondaryConfig.StartBands?.Count ?? 0, "Custom start bands should survive");
        Assert.AreEqual("custom", secondaryConfig.StartBands![0].ProviderId);
    }

    [TestMethod]
    public void Reconcile_StaleConfig_PrunedAfterSixMonths()
    {
        var now = DateTime.UtcNow;
        var sevenMonthsAgo = now - TimeSpan.FromDays(210);
        var fiveMonthsAgo = now - TimeSpan.FromDays(150);

        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true, LastSeen = now },
            new DockMonitorConfig { MonitorDeviceId = @"\\?\DISPLAY#STALE#4&sss&0&UID444#{guidStale}", Enabled = true, IsPrimary = false, LastSeen = sevenMonthsAgo },
            new DockMonitorConfig { MonitorDeviceId = @"\\?\DISPLAY#RECENT#4&rrr&0&UID555#{guidRecent}", Enabled = true, IsPrimary = false, LastSeen = fiveMonthsAgo });

        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors, now);

        // Primary is matched, RECENT is retained (< 6 months), STALE is pruned (> 6 months)
        Assert.AreEqual(2, result.Count, "Should have matched primary + recently-seen disconnected config");
        Assert.AreEqual(PrimaryMonitor.StableId, result[0].MonitorDeviceId);
        Assert.AreEqual(@"\\?\DISPLAY#RECENT#4&rrr&0&UID555#{guidRecent}", result[1].MonitorDeviceId, "Recently-seen config should be retained");
    }

    [TestMethod]
    public void Reconcile_LegacyConfigWithoutLastSeen_TreatedAsFresh()
    {
        // Configs from before LastSeen was added (LastSeen is null)
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, Enabled = true, IsPrimary = true },
            new DockMonitorConfig { MonitorDeviceId = @"\\?\DISPLAY#LEGACY#4&lll&0&UID666#{guidLegacy}", Enabled = true, IsPrimary = false, IsCustomized = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        // Legacy config (null LastSeen) should be treated as fresh and retained
        Assert.AreEqual(2, result.Count, "Legacy config without LastSeen should be retained");
        Assert.AreEqual(@"\\?\DISPLAY#LEGACY#4&lll&0&UID666#{guidLegacy}", result[1].MonitorDeviceId);
    }

    [TestMethod]
    public void Reconciler_LegacyGdiName_MigratedToStableId()
    {
        // Simulate upgrade from pre-stable-ID settings: configs use GDI device names
        var configs = ImmutableList.Create(
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, IsPrimary = true, Side = DockSide.Left },
            new DockMonitorConfig { MonitorDeviceId = @"\\.\DISPLAY2", Enabled = true, IsPrimary = false, IsCustomized = true });

        var monitors = new List<MonitorInfo> { PrimaryMonitor, SecondaryMonitor };

        var result = MonitorConfigReconciler.Reconcile(configs, monitors);

        // Phase 1.5 should detect GDI-style names and rewrite to stable IDs
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(PrimaryMonitor.StableId, result[0].MonitorDeviceId, "Primary should be migrated to stable ID");
        Assert.AreEqual(SecondaryMonitor.StableId, result[1].MonitorDeviceId, "Secondary should be migrated to stable ID");
        Assert.AreEqual(DockSide.Left, result[0].Side, "Side override should survive migration");
        Assert.IsTrue(result[1].IsCustomized, "Customization flag should survive migration");
    }

    private static SettingsModel CreateSettingsModelWithConfigs(params DockMonitorConfig[] configs)
    {
        var dockSettings = CreateMinimalDockSettings() with
        {
            MonitorConfigs = ImmutableList.Create(configs),
        };

        var minimalJson = "{}";
        var settingsModel = JsonSerializer.Deserialize(
            minimalJson,
            JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();

        return settingsModel with { DockSettings = dockSettings };
    }

    [TestMethod]
    public void FreshInstall_MonitorConfigs_SurvivesRecordWithExpression()
    {
        // Simulate fresh install: JSON has no MonitorConfigs key at all.
        // System.Text.Json passes null to the init setter. Verify the backing field is
        // properly coalesced so that record `with` clones remain non-null.
        var json = "{}";
        var deserialized = JsonSerializer.Deserialize(
            json,
            JsonSerializationContext.Default.DockSettings) ?? new DockSettings();

        // Direct read through getter should be non-null
        Assert.IsNotNull(deserialized.MonitorConfigs, "MonitorConfigs should never be null after deserialization");
        Assert.AreEqual(0, deserialized.MonitorConfigs.Count);

        // Crucially: a `with` clone must also have non-null MonitorConfigs
        var clone = deserialized with { ShowLabels = false };
        Assert.IsNotNull(clone.MonitorConfigs, "MonitorConfigs should survive record 'with' expression");
        Assert.AreEqual(0, clone.MonitorConfigs.Count);

        // Double-clone to be thorough
        var clone2 = clone with { Side = DockSide.Left };
        Assert.IsNotNull(clone2.MonitorConfigs, "MonitorConfigs should survive multiple 'with' expressions");
    }

    private static Mock<ISettingsService> CreateMockSettingsService(SettingsModel settings)
    {
        var mock = new Mock<ISettingsService>();
        mock.Setup(s => s.Settings).Returns(settings);
        mock.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, _) =>
            {
                var updated = transform(settings);
                mock.Setup(s => s.Settings).Returns(updated);
            });
        return mock;
    }
}
