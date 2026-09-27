// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

    [TestMethod]
    public void Resolve_NoDocks_ReturnsNull()
    {
        var result = DockFocusTargetResolver.Resolve(BothMonitors, [], 100, 100);

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
    public void DefaultDockFocusShortcut_IsWinAltD()
    {
        var shortcut = SettingsModel.DefaultDockFocusShortcut;

        Assert.IsTrue(shortcut.Win);
        Assert.IsTrue(shortcut.Alt);
        Assert.IsFalse(shortcut.Ctrl);
        Assert.IsFalse(shortcut.Shift);
        Assert.AreEqual(0x44, shortcut.Code);
    }

    [TestMethod]
    public void DockFocusHotkey_DefaultsToWinAltD()
    {
        var settings = new SettingsModel();

        Assert.AreEqual(SettingsModel.DefaultDockFocusShortcut, settings.DockFocusHotkey);
    }

    [TestMethod]
    public void DockFocusHotkey_SurvivesJsonRoundTrip()
    {
        var settings = new SettingsModel
        {
            DockFocusHotkey = new HotkeySettings(false, true, true, false, 0x4A),
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
