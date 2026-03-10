// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockMultiMonitorTests
{
    private static MonitorInfo MakeMonitor(
        string deviceId = @"\\.\DISPLAY1",
        string displayName = "Test Monitor",
        uint dpi = 96,
        bool isPrimary = true,
        int left = 0,
        int top = 0,
        int right = 1920,
        int bottom = 1080)
    {
        var bounds = new ScreenRect(left, top, right, bottom);
        return new MonitorInfo
        {
            DeviceId = deviceId,
            DisplayName = displayName,
            Bounds = bounds,
            WorkArea = bounds,
            Dpi = dpi,
            IsPrimary = isPrimary,
        };
    }

    // ScreenRect tests
    [TestMethod]
    public void ScreenRect_Width_IsRightMinusLeft()
    {
        var rect = new ScreenRect(100, 50, 1920, 1080);
        Assert.AreEqual(1820, rect.Width);
    }

    [TestMethod]
    public void ScreenRect_Height_IsBottomMinusTop()
    {
        var rect = new ScreenRect(100, 50, 1920, 1080);
        Assert.AreEqual(1030, rect.Height);
    }

    [TestMethod]
    public void ScreenRect_ZeroSize_WhenLeftEqualsRight()
    {
        var rect = new ScreenRect(500, 200, 500, 200);
        Assert.AreEqual(0, rect.Width);
        Assert.AreEqual(0, rect.Height);
    }

    [TestMethod]
    public void ScreenRect_NegativeOrigin_ComputesCorrectly()
    {
        // Secondary monitor to the left of primary
        var rect = new ScreenRect(-1920, 0, 0, 1080);
        Assert.AreEqual(1920, rect.Width);
        Assert.AreEqual(1080, rect.Height);
    }

    [TestMethod]
    public void ScreenRect_Equality_SameValues()
    {
        var a = new ScreenRect(0, 0, 1920, 1080);
        var b = new ScreenRect(0, 0, 1920, 1080);
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void ScreenRect_Inequality_DifferentValues()
    {
        var a = new ScreenRect(0, 0, 1920, 1080);
        var b = new ScreenRect(0, 0, 2560, 1440);
        Assert.AreNotEqual(a, b);
    }

    // MonitorInfo tests
    [TestMethod]
    public void MonitorInfo_ScaleFactor_96Dpi_Returns1()
    {
        var monitor = MakeMonitor(dpi: 96);
        Assert.AreEqual(1.0, monitor.ScaleFactor, 0.001);
    }

    [TestMethod]
    public void MonitorInfo_ScaleFactor_120Dpi_Returns1_25()
    {
        var monitor = MakeMonitor(dpi: 120);
        Assert.AreEqual(1.25, monitor.ScaleFactor, 0.001);
    }

    [TestMethod]
    public void MonitorInfo_ScaleFactor_144Dpi_Returns1_5()
    {
        var monitor = MakeMonitor(dpi: 144);
        Assert.AreEqual(1.5, monitor.ScaleFactor, 0.001);
    }

    [TestMethod]
    public void MonitorInfo_ScaleFactor_192Dpi_Returns2()
    {
        var monitor = MakeMonitor(dpi: 192);
        Assert.AreEqual(2.0, monitor.ScaleFactor, 0.001);
    }

    [TestMethod]
    public void MonitorInfo_RecordEquality_SameValues()
    {
        var a = MakeMonitor(deviceId: "A", dpi: 96);
        var b = MakeMonitor(deviceId: "A", dpi: 96);
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void MonitorInfo_RecordInequality_DifferentDpi()
    {
        var a = MakeMonitor(deviceId: "A", dpi: 96);
        var b = MakeMonitor(deviceId: "A", dpi: 192);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void MonitorInfo_RecordInequality_DifferentDeviceId()
    {
        var a = MakeMonitor(deviceId: "A");
        var b = MakeMonitor(deviceId: "B");
        Assert.AreNotEqual(a, b);
    }

    // DockMonitorConfig.ResolveSide tests
    [TestMethod]
    public void ResolveSide_ReturnsPerMonitorSide_WhenSet()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = "DISPLAY1",
            Enabled = true,
            Side = DockSide.Left,
        };

        Assert.AreEqual(DockSide.Left, config.ResolveSide(DockSide.Top));
    }

    [TestMethod]
    public void ResolveSide_FallsBackToDefault_WhenNull()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = "DISPLAY1",
            Enabled = true,
            Side = null,
        };

        Assert.AreEqual(DockSide.Bottom, config.ResolveSide(DockSide.Bottom));
    }

    [TestMethod]
    [DataRow(DockSide.Left)]
    [DataRow(DockSide.Top)]
    [DataRow(DockSide.Right)]
    [DataRow(DockSide.Bottom)]
    public void ResolveSide_AllEnumValues_ReturnedWhenSetExplicitly(DockSide side)
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = "DISPLAY1",
            Side = side,
        };

        Assert.AreEqual(side, config.ResolveSide(DockSide.Top));
    }

    [TestMethod]
    [DataRow(DockSide.Left)]
    [DataRow(DockSide.Top)]
    [DataRow(DockSide.Right)]
    [DataRow(DockSide.Bottom)]
    public void ResolveSide_AllEnumValues_UsedAsFallback(DockSide defaultSide)
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = "DISPLAY1",
            Side = null,
        };

        Assert.AreEqual(defaultSide, config.ResolveSide(defaultSide));
    }

    [TestMethod]
    public void ResolveSide_PerMonitorOverridesDefault_EvenWhenSame()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = "DISPLAY1",
            Side = DockSide.Top,
        };

        // Even when the per-monitor value matches the default, ResolveSide
        // returns the explicit per-monitor value (not a fallback).
        Assert.AreEqual(DockSide.Top, config.ResolveSide(DockSide.Top));
    }

    // DockMonitorConfig.Enabled tests
    [TestMethod]
    public void DockMonitorConfig_EnabledDefaultsToTrue()
    {
        var config = new DockMonitorConfig { MonitorDeviceId = "X" };
        Assert.IsTrue(config.Enabled);
    }

    [TestMethod]
    public void DockMonitorConfig_CanBeDisabled()
    {
        var config = new DockMonitorConfig
        {
            MonitorDeviceId = "X",
            Enabled = false,
        };

        Assert.IsFalse(config.Enabled);
    }

    // DockSettings.MonitorConfigs default state
    [TestMethod]
    public void DockSettings_MonitorConfigs_DefaultsToEmpty()
    {
        var settings = new DockSettings();
        Assert.AreEqual(0, settings.MonitorConfigs.Count);
    }

    [TestMethod]
    public void DockSettings_DefaultSide_IsTop()
    {
        var settings = new DockSettings();
        Assert.AreEqual(DockSide.Top, settings.Side);
    }

    // GetEffectiveConfigs logic (tested via data shape)
    //
    // DockWindowManager.GetEffectiveConfigs is private, but the core
    // fallback behavior is: "when MonitorConfigs is empty, treat the
    // primary monitor as the single enabled config with Side = null".
    // We verify this contract through the config list itself.
    [TestMethod]
    public void EffectiveConfigs_WhenConfigsExist_ReturnsThemDirectly()
    {
        var dockSettings = new DockSettings();
        dockSettings.MonitorConfigs.Add(new DockMonitorConfig
        {
            MonitorDeviceId = "MON1",
            Enabled = true,
            Side = DockSide.Left,
        });
        dockSettings.MonitorConfigs.Add(new DockMonitorConfig
        {
            MonitorDeviceId = "MON2",
            Enabled = false,
        });

        // The manager should use MonitorConfigs as-is when non-empty.
        Assert.AreEqual(2, dockSettings.MonitorConfigs.Count);
        Assert.AreEqual("MON1", dockSettings.MonitorConfigs[0].MonitorDeviceId);
        Assert.IsTrue(dockSettings.MonitorConfigs[0].Enabled);
        Assert.AreEqual("MON2", dockSettings.MonitorConfigs[1].MonitorDeviceId);
        Assert.IsFalse(dockSettings.MonitorConfigs[1].Enabled);
    }

    [TestMethod]
    public void EffectiveConfigs_WhenConfigsEmpty_FallbackShouldUsePrimary()
    {
        // When MonitorConfigs is empty the manager synthesizes a config
        // for the primary monitor with Enabled=true and Side=null.
        // We verify the data contract that the fallback config should use.
        var dockSettings = new DockSettings();
        Assert.AreEqual(0, dockSettings.MonitorConfigs.Count);

        // The synthesized config should inherit the global Side via null.
        var synthetic = new DockMonitorConfig
        {
            MonitorDeviceId = "PRIMARY",
            Enabled = true,
            Side = null,
        };

        Assert.IsTrue(synthetic.Enabled);
        Assert.IsNull(synthetic.Side);
        Assert.AreEqual(DockSide.Top, synthetic.ResolveSide(dockSettings.Side));
    }

    [TestMethod]
    public void EffectiveConfigs_MultipleMonitors_OnlyEnabledAreActive()
    {
        var configs = new List<DockMonitorConfig>
        {
            new() { MonitorDeviceId = "A", Enabled = true },
            new() { MonitorDeviceId = "B", Enabled = false },
            new() { MonitorDeviceId = "C", Enabled = true, Side = DockSide.Right },
        };

        // Simulates the ShowDocks filter: only enabled configs are acted upon.
        var active = configs.Where(c => c.Enabled).ToList();
        Assert.AreEqual(2, active.Count);
        Assert.AreEqual("A", active[0].MonitorDeviceId);
        Assert.AreEqual("C", active[1].MonitorDeviceId);
    }

    [TestMethod]
    public void EffectiveConfigs_SynthesizedConfig_InheritsGlobalSide()
    {
        // Verifies the full fallback chain:
        // Empty MonitorConfigs → synthesize with Side=null → ResolveSide returns global
        var dockSettings = new DockSettings { Side = DockSide.Right };

        var synthetic = new DockMonitorConfig
        {
            MonitorDeviceId = "PRIMARY",
            Enabled = true,
            Side = null,
        };

        Assert.AreEqual(DockSide.Right, synthetic.ResolveSide(dockSettings.Side));
    }

    // DockBandSettings.ResolveShowTitles / ResolveShowSubtitles
    // (related resolve-with-fallback pattern)
    [TestMethod]
    public void DockBandSettings_ResolveShowTitles_UsesExplicitValue()
    {
        var band = new DockBandSettings
        {
            ProviderId = "P",
            CommandId = "C",
            ShowTitles = false,
        };

        Assert.IsFalse(band.ResolveShowTitles(defaultValue: true));
    }

    [TestMethod]
    public void DockBandSettings_ResolveShowTitles_FallsBackToDefault()
    {
        var band = new DockBandSettings
        {
            ProviderId = "P",
            CommandId = "C",
            ShowTitles = null,
        };

        Assert.IsTrue(band.ResolveShowTitles(defaultValue: true));
    }

    [TestMethod]
    public void DockBandSettings_ResolveShowSubtitles_FallsBackToDefault()
    {
        var band = new DockBandSettings
        {
            ProviderId = "P",
            CommandId = "C",
            ShowSubtitles = null,
        };

        Assert.IsFalse(band.ResolveShowSubtitles(defaultValue: false));
    }

    // MonitorConfigReconciler tests
    [TestMethod]
    public void Reconciler_ExactMatch_NoChanges()
    {
        var monitors = new List<MonitorInfo>
        {
            MakeMonitor(@"\\.\DISPLAY1", isPrimary: true),
            MakeMonitor(@"\\.\DISPLAY2", isPrimary: false, left: 1920, right: 3840),
        };
        var configs = new List<DockMonitorConfig>
        {
            new() { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, Side = DockSide.Bottom, IsPrimary = true },
            new() { MonitorDeviceId = @"\\.\DISPLAY2", Enabled = true, Side = DockSide.Right, IsPrimary = false },
        };

        var changed = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.IsFalse(changed);
        Assert.AreEqual(2, configs.Count);
    }

    [TestMethod]
    public void Reconciler_StaleDeviceIds_ReassociatesByIsPrimary()
    {
        // Simulate reboot: DISPLAY1 → DISPLAY49, DISPLAY2 → DISPLAY50
        var monitors = new List<MonitorInfo>
        {
            MakeMonitor(@"\\.\DISPLAY49", isPrimary: true),
            MakeMonitor(@"\\.\DISPLAY50", isPrimary: false, left: 1920, right: 3840),
        };
        var configs = new List<DockMonitorConfig>
        {
            new() { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, Side = DockSide.Top, IsPrimary = true },
            new() { MonitorDeviceId = @"\\.\DISPLAY2", Enabled = true, Side = DockSide.Right, IsPrimary = false },
        };

        var changed = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, configs.Count);

        var primary = configs.First(c => c.IsPrimary);
        Assert.AreEqual(@"\\.\DISPLAY49", primary.MonitorDeviceId);
        Assert.AreEqual(DockSide.Top, primary.Side);
        Assert.IsTrue(primary.Enabled);

        var secondary = configs.First(c => !c.IsPrimary);
        Assert.AreEqual(@"\\.\DISPLAY50", secondary.MonitorDeviceId);
        Assert.AreEqual(DockSide.Right, secondary.Side);
        Assert.IsTrue(secondary.Enabled);
    }

    [TestMethod]
    public void Reconciler_OrphanedConfigs_AreRemoved()
    {
        // 3 stale configs from prior sessions, 2 current monitors
        var monitors = new List<MonitorInfo>
        {
            MakeMonitor(@"\\.\DISPLAY113", isPrimary: true),
            MakeMonitor(@"\\.\DISPLAY114", isPrimary: false, left: 1920, right: 3840),
        };
        var configs = new List<DockMonitorConfig>
        {
            new() { MonitorDeviceId = @"\\.\DISPLAY49", Enabled = true, Side = DockSide.Top, IsPrimary = true },
            new() { MonitorDeviceId = @"\\.\DISPLAY50", Enabled = true, Side = DockSide.Right, IsPrimary = false },
            new() { MonitorDeviceId = @"\\.\DISPLAY81", Enabled = true, Side = DockSide.Top, IsPrimary = true },
            new() { MonitorDeviceId = @"\\.\DISPLAY82", Enabled = false, Side = null, IsPrimary = false },
        };

        var changed = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.IsTrue(changed);

        // Should have exactly 2 configs after reconciliation
        Assert.AreEqual(2, configs.Count);
        Assert.IsTrue(configs.Any(c => c.MonitorDeviceId == @"\\.\DISPLAY113"));
        Assert.IsTrue(configs.Any(c => c.MonitorDeviceId == @"\\.\DISPLAY114"));
    }

    [TestMethod]
    public void Reconciler_NewMonitor_GetsDefaultConfig()
    {
        // One existing monitor, one brand new monitor
        var monitors = new List<MonitorInfo>
        {
            MakeMonitor(@"\\.\DISPLAY1", isPrimary: true),
            MakeMonitor(@"\\.\DISPLAY3", isPrimary: false, left: 1920, right: 3840),
        };
        var configs = new List<DockMonitorConfig>
        {
            new() { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, Side = DockSide.Bottom, IsPrimary = true },
        };

        var changed = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, configs.Count);

        var newConfig = configs.First(c => c.MonitorDeviceId == @"\\.\DISPLAY3");
        Assert.IsFalse(newConfig.Enabled); // Non-primary defaults to disabled
        Assert.IsNull(newConfig.Side);     // Inherits global side
        Assert.IsFalse(newConfig.IsPrimary);
    }

    [TestMethod]
    public void Reconciler_UpdatesIsPrimaryWhenMonitorChanges()
    {
        // Config was saved as primary, but monitor is now secondary
        var monitors = new List<MonitorInfo>
        {
            MakeMonitor(@"\\.\DISPLAY1", isPrimary: false),
        };
        var configs = new List<DockMonitorConfig>
        {
            new() { MonitorDeviceId = @"\\.\DISPLAY1", Enabled = true, IsPrimary = true },
        };

        var changed = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.IsTrue(changed);
        Assert.IsFalse(configs[0].IsPrimary);
    }

    [TestMethod]
    public void Reconciler_EmptyConfigs_CreatesDefaultsForAllMonitors()
    {
        var monitors = new List<MonitorInfo>
        {
            MakeMonitor(@"\\.\DISPLAY1", isPrimary: true),
            MakeMonitor(@"\\.\DISPLAY2", isPrimary: false, left: 1920, right: 3840),
        };
        var configs = new List<DockMonitorConfig>();

        var changed = MonitorConfigReconciler.Reconcile(configs, monitors);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, configs.Count);

        var primary = configs.First(c => c.IsPrimary);
        Assert.IsTrue(primary.Enabled);

        var secondary = configs.First(c => !c.IsPrimary);
        Assert.IsFalse(secondary.Enabled);
    }
}
