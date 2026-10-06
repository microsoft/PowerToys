// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockFocusShortcutTests
{
    private static readonly MonitorInfo PrimaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY1",
        StableId = "primary-id",
        DisplayName = "Display 1 (Primary)",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = true,
    };

    private static readonly MonitorInfo SecondaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY2",
        StableId = "secondary-id",
        DisplayName = "Display 2",
        Bounds = new ScreenRect(1920, 0, 3840, 1080),
        WorkArea = new ScreenRect(1920, 0, 3840, 1040),
        Dpi = 96,
        IsPrimary = false,
    };

    private static readonly List<MonitorInfo> BothMonitors = [PrimaryMonitor, SecondaryMonitor];

    private static readonly MonitorInfo LeftMonitor = PrimaryMonitor with
    {
        DeviceId = @"\\.\DISPLAY3",
        StableId = "left-id",
        Bounds = new ScreenRect(-1920, 0, 0, 1080),
        WorkArea = new ScreenRect(-1920, 0, 0, 1040),
        IsPrimary = false,
    };

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Resolve_NoDocks_ReturnsNull(bool focusPrimaryFirst)
    {
        var result = DockFocusTargetResolver.Resolve(BothMonitors, [], 100, 100, focusPrimaryFirst);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_CursorOnMonitorWithDock_ReturnsThatMonitor()
    {
        var result = DockFocusTargetResolver.Resolve(
            BothMonitors,
            ["primary-id", "secondary-id"],
            2000,
            500);

        Assert.AreEqual("secondary-id", result);
    }

    [TestMethod]
    [DataRow(false, "secondary-id")]
    [DataRow(true, "primary-id")]
    public void Resolve_PrimaryFirstOption_ControlsStartingDock(bool focusPrimaryFirst, string expectedDock)
    {
        var result = DockFocusTargetResolver.Resolve(
            BothMonitors,
            ["secondary-id", "primary-id"],
            2000,
            500,
            focusPrimaryFirst);

        Assert.AreEqual(expectedDock, result);
    }

    [TestMethod]
    public void Resolve_PrimaryFirstWithoutPrimaryDock_FallsBackToCursorMonitor()
    {
        var result = DockFocusTargetResolver.Resolve(
            [LeftMonitor, PrimaryMonitor, SecondaryMonitor],
            ["left-id", "secondary-id"],
            2000,
            500,
            focusPrimaryFirst: true);

        Assert.AreEqual("secondary-id", result);
    }

    [TestMethod]
    public void Resolve_PrimaryFirstWithoutPrimaryMonitor_FallsBackToCursorMonitor()
    {
        var result = DockFocusTargetResolver.Resolve(
            [LeftMonitor, SecondaryMonitor],
            ["left-id", "secondary-id"],
            2000,
            500,
            focusPrimaryFirst: true);

        Assert.AreEqual("secondary-id", result);
    }

    [TestMethod]
    public void Resolve_PrimaryFirst_MatchesMonitorIdsWithoutCaseSensitivity()
    {
        var result = DockFocusTargetResolver.Resolve(
            BothMonitors,
            ["SECONDARY-ID", "PRIMARY-ID"],
            2000,
            500,
            focusPrimaryFirst: true);

        Assert.AreEqual("PRIMARY-ID", result);
    }

    [TestMethod]
    public void Resolve_CursorOnMonitorWithoutDock_FallsBackToPrimary()
    {
        var result = DockFocusTargetResolver.Resolve(
            BothMonitors,
            ["primary-id"],
            2000,
            500);

        Assert.AreEqual("primary-id", result);
    }

    [TestMethod]
    public void Resolve_PrimaryHasNoDock_FallsBackToTheOneThatDoes()
    {
        var result = DockFocusTargetResolver.Resolve(
            BothMonitors,
            ["secondary-id"],
            -5000,
            -5000);

        Assert.AreEqual("secondary-id", result);
    }

    [TestMethod]
    public void Resolve_CursorOffAllMonitors_FallsBackToPrimary()
    {
        var result = DockFocusTargetResolver.Resolve(
            BothMonitors,
            ["primary-id", "secondary-id"],
            -5000,
            -5000);

        Assert.AreEqual("primary-id", result);
    }

    [TestMethod]
    [DataRow("primary-id", "primary-id", "secondary-id", "left-id")]
    [DataRow("secondary-id", "secondary-id", "left-id", "primary-id")]
    [DataRow("left-id", "left-id", "primary-id", "secondary-id")]
    [DataRow("disconnected-id", "left-id", "primary-id", "secondary-id")]
    public void GetTraversalOrder_TraversesSpatiallyFromCurrentDock(
        string currentId, string firstId, string secondId, string thirdId)
    {
        var result = DockFocusTargetResolver.GetTraversalOrder(
            [SecondaryMonitor, LeftMonitor, PrimaryMonitor],
            ["secondary-id", "primary-id", "left-id"],
            currentId);

        CollectionAssert.AreEqual(new[] { firstId, secondId, thirdId }, result.ToArray());
    }

    [TestMethod]
    [DataRow("primary-id", "primary-id", "left-id", "secondary-id")]
    [DataRow("secondary-id", "secondary-id", "primary-id", "left-id")]
    [DataRow("left-id", "left-id", "secondary-id", "primary-id")]
    [DataRow("disconnected-id", "secondary-id", "primary-id", "left-id")]
    public void GetTraversalOrder_Reverse_TraversesSpatiallyFromCurrentDock(
        string currentId, string firstId, string secondId, string thirdId)
    {
        var result = DockFocusTargetResolver.GetTraversalOrder(
            [SecondaryMonitor, LeftMonitor, PrimaryMonitor],
            ["secondary-id", "primary-id", "left-id"],
            currentId,
            reverse: true);

        CollectionAssert.AreEqual(new[] { firstId, secondId, thirdId }, result.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetTraversalOrder_ExcludesDisabledAndDisconnectedDocks(bool reverse)
    {
        string[] expected = ["secondary-id", "left-id"];

        var result = DockFocusTargetResolver.GetTraversalOrder(
            [SecondaryMonitor, LeftMonitor, PrimaryMonitor],
            ["secondary-id", "disconnected-id", "left-id"],
            "secondary-id",
            reverse);

        CollectionAssert.AreEqual(expected, result.ToArray());
    }

    [TestMethod]
    public void GetTraversalOrder_StackedMonitorsAreOrderedTopToBottom()
    {
        var above = SecondaryMonitor with
        {
            Bounds = new ScreenRect(0, -1080, 1920, 0),
        };
        string[] expected = ["left-id", "secondary-id", "primary-id"];

        var result = DockFocusTargetResolver.GetTraversalOrder(
            [PrimaryMonitor, above, LeftMonitor],
            ["primary-id", "secondary-id", "left-id"],
            "left-id");

        CollectionAssert.AreEqual(expected, result.ToArray());
    }

    [TestMethod]
    public void GetTraversalOrder_MatchesMonitorIdsWithoutCaseSensitivity()
    {
        string[] expected = ["SECONDARY-ID", "PRIMARY-ID"];

        var result = DockFocusTargetResolver.GetTraversalOrder(
            BothMonitors,
            ["PRIMARY-ID", "SECONDARY-ID"],
            "secondary-id");

        CollectionAssert.AreEqual(expected, result.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetTraversalOrder_NoConnectedDocks_ReturnsEmpty(bool reverse)
    {
        var result = DockFocusTargetResolver.GetTraversalOrder(BothMonitors, ["disconnected-id"], "disconnected-id", reverse);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void DockFocusPrimaryFirst_DefaultsToDisabled()
    {
        Assert.IsFalse(new SettingsModel().DockFocusPrimaryFirst);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"DockFocusAcrossMonitors":true,"DockRememberLastFocusedItem":true}""")]
    public void DockFocusPrimaryFirst_UnsetInJson_DefaultsToDisabled(string json)
    {
        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        Assert.IsFalse(settings.DockFocusPrimaryFirst);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DockFocusPrimaryFirst_SurvivesJsonRoundTrip(bool enabled)
    {
        var settings = new SettingsModel { DockFocusPrimaryFirst = enabled };
        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var roundTripped = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(enabled, roundTripped.DockFocusPrimaryFirst);
        Assert.IsTrue(roundTripped.DockFocusAcrossMonitors);
        Assert.IsTrue(roundTripped.DockRememberLastFocusedItem);
    }

    [TestMethod]
    public void DockFocusAcrossMonitors_DefaultsToEnabled()
    {
        Assert.IsTrue(new SettingsModel().DockFocusAcrossMonitors);
    }

    [TestMethod]
    [DataRow("{}", true)]
    [DataRow("""{"DockRememberLastFocusedItem":false}""", false)]
    public void DockFocusAcrossMonitors_UnsetInJson_DefaultsToEnabled(string json, bool rememberLastFocusedItem)
    {
        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.DockFocusAcrossMonitors);
        Assert.AreEqual(rememberLastFocusedItem, settings.DockRememberLastFocusedItem);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DockFocusAcrossMonitors_SurvivesJsonRoundTrip(bool enabled)
    {
        var settings = new SettingsModel { DockFocusAcrossMonitors = enabled };
        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var roundTripped = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(enabled, roundTripped.DockFocusAcrossMonitors);
    }

    [TestMethod]
    public void DockRememberLastFocusedItem_DefaultsToEnabled()
    {
        Assert.IsTrue(new SettingsModel().DockRememberLastFocusedItem);
    }

    [TestMethod]
    [DataRow("{}", true)]
    [DataRow("""{"DockFocusAcrossMonitors":false}""", false)]
    public void DockRememberLastFocusedItem_UnsetInJson_DefaultsToEnabled(string json, bool focusAcrossMonitors)
    {
        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.DockRememberLastFocusedItem);
        Assert.AreEqual(focusAcrossMonitors, settings.DockFocusAcrossMonitors);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DockRememberLastFocusedItem_SurvivesJsonRoundTrip(bool enabled)
    {
        var settings = new SettingsModel { DockRememberLastFocusedItem = enabled };
        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var roundTripped = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(enabled, roundTripped.DockRememberLastFocusedItem);
    }

    [TestMethod]
    public void DefaultDockFocusShortcut_IsWinAltJ()
    {
        var shortcut = SettingsModel.DefaultDockFocusShortcut;

        Assert.IsTrue(shortcut.Win);
        Assert.IsTrue(shortcut.Alt);
        Assert.IsFalse(shortcut.Ctrl);
        Assert.IsFalse(shortcut.Shift);
        Assert.AreEqual(0x4A, shortcut.Code);
    }

    [TestMethod]
    public void DockFocusHotkey_DefaultsToWinAltJ()
    {
        var settings = new SettingsModel();

        Assert.AreEqual(SettingsModel.DefaultDockFocusShortcut, settings.DockFocusHotkey);
    }

    [TestMethod]
    public void DockFocusHotkey_SurvivesJsonRoundTrip()
    {
        var settings = new SettingsModel
        {
            DockFocusHotkey = new HotkeySettings(false, true, true, false, 0x4B),
        };

        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var roundTripped = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(settings.DockFocusHotkey, roundTripped.DockFocusHotkey);
    }

    [TestMethod]
    public void DockFocusHotkey_UnsetInJson_FallsBackToDefault()
    {
        var roundTripped = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(SettingsModel.DefaultDockFocusShortcut, roundTripped.DockFocusHotkey);
    }

    [TestMethod]
    public void IsDockHotkey_OnlyMatchesDockIds()
    {
        Assert.IsTrue(DockHotkeyIds.IsDockHotkey(DockHotkeyIds.FocusDock));
        Assert.IsFalse(DockHotkeyIds.IsDockHotkey(string.Empty));
        Assert.IsFalse(DockHotkeyIds.IsDockHotkey("com.contoso.some.command"));
    }
}
