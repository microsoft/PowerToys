// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DashboardSettingsTests
{
    [TestMethod]
    public void Defaults_DashboardIsOptIn()
    {
        var settings = new SettingsModel();

        Assert.IsFalse(settings.EnableDashboard);
        Assert.IsTrue(settings.DashboardSettings.IgnoreHotkeyWhenFullscreen);
        Assert.IsTrue(settings.DashboardSettings.DismissOnFocusLost);
        Assert.AreEqual(0, settings.DashboardSettings.Tiles.Count);
    }

    [TestMethod]
    public void WithTilePinned_AddsAnUnplacedTileOnce()
    {
        var settings = new DashboardSettings().WithTilePinned("provider", "page");
        var again = settings.WithTilePinned("provider", "page");

        Assert.AreSame(settings, again);
        Assert.AreEqual(1, settings.Tiles.Count);
        Assert.IsTrue(settings.IsPinned("provider", "page"));
        Assert.IsFalse(settings.Tiles[0].Rect.IsPlaced);
    }

    [TestMethod]
    public void WithTileUnpinned_RemovesOnlyThatTile()
    {
        var settings = new DashboardSettings()
            .WithTilePinned("p", "a")
            .WithTilePinned("p", "b")
            .WithTileUnpinned("p", "a");

        Assert.AreEqual(1, settings.Tiles.Count);
        Assert.IsTrue(settings.IsPinned("p", "b"));
        Assert.AreSame(settings, settings.WithTileUnpinned("p", "missing"));
    }

    [TestMethod]
    public void WithLayout_RecordsRectsAndDesignGrid()
    {
        var settings = new DashboardSettings()
            .WithTilePinned("p", "a")
            .WithTilePinned("p", "b")
            .WithLayout([new(1, 2, 20, 10), new(30, 4, 12, 16)], columns: 96, rows: 54);

        Assert.AreEqual(new DashboardTileRect(1, 2, 20, 10), settings.Tiles[0].Rect);
        Assert.AreEqual(new DashboardTileRect(30, 4, 12, 16), settings.Tiles[1].Rect);
        Assert.AreEqual(96, settings.DesignColumns);
        Assert.AreEqual(54, settings.DesignRows);
    }

    [TestMethod]
    public void WithLayout_RequiresOneRectPerTile()
    {
        var settings = new DashboardSettings().WithTilePinned("p", "a");

        Assert.ThrowsException<ArgumentException>(() => settings.WithLayout([], 10, 10));
    }

    [TestMethod]
    public void WithLayoutReset_UnplacesEveryTile()
    {
        var settings = new DashboardSettings()
            .WithTilePinned("p", "a")
            .WithLayout([new(5, 5, 40, 40)], 96, 54)
            .WithLayoutReset();

        Assert.IsFalse(settings.Tiles[0].Rect.IsPlaced);
        Assert.AreEqual(DashboardLayout.DefaultColumnSpan, settings.Tiles[0].ColumnSpan);
        Assert.AreEqual(DashboardLayout.DefaultRowSpan, settings.Tiles[0].RowSpan);
        Assert.AreEqual(0, settings.DesignColumns);
    }

    [TestMethod]
    public void Equality_ComparesTilesByContent()
    {
        var a = new DashboardSettings().WithTilePinned("p", "a");
        var b = new DashboardSettings().WithTilePinned("p", "a");

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, b.WithTilePinned("p", "b"));
    }

    [TestMethod]
    public void SettingsModel_RoundTripsDashboardSettings()
    {
        var original = new SettingsModel
        {
            EnableDashboard = true,
            DashboardSettings = new DashboardSettings
            {
                IgnoreHotkeyWhenFullscreen = false,
                DismissOnFocusLost = false,
                Backdrop = DashboardBackdrop.Acrylic,
                BackgroundOpacity = 70,
            }
            .WithTilePinned("provider", "page")
            .WithLayout([new(3, 4, 22, 18)], 80, 45),
        };

        var json = JsonSerializer.Serialize(original, JsonSerializationContext.Default.SettingsModel);
        var restored = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.EnableDashboard);
        Assert.AreEqual(original.DashboardHotkey?.ToString(), restored.DashboardHotkey?.ToString());
        Assert.AreEqual(original.DashboardSettings, restored.DashboardSettings);
    }

    [TestMethod]
    public void SettingsModel_MissingDashboardSettings_UsesDefaults()
    {
        var restored = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(restored);
        Assert.IsFalse(restored.EnableDashboard);
        Assert.IsNotNull(restored.DashboardSettings);
        Assert.AreEqual(0, restored.DashboardSettings.Tiles.Count);
    }
}
