// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies that <see cref="DockSettings"/> (and its nested <see cref="DockMonitorConfig"/>)
/// compare by <em>content</em> rather than by list reference. Dock consumers guard their
/// (expensive) reloads with <c>_settings == args.DockSettings</c>, so two settings that differ
/// only by having freshly-rebuilt band lists — e.g. after loading from disk — must compare
/// equal, otherwise the reload fires needlessly.
/// </summary>
[TestClass]
public class DockSettingsEqualityTests
{
    private static DockBandSettings Band(string providerId, string commandId, bool? showTitles = null) =>
        new() { ProviderId = providerId, CommandId = commandId, ShowTitles = showTitles };

    [TestMethod]
    public void Equal_WhenBandListsHaveSameContentButDifferentInstances()
    {
        var a = new DockSettings
        {
            StartBands = ImmutableList.Create(Band("p", "home"), Band("w", "winget", showTitles: false)),
        };
        var b = new DockSettings
        {
            StartBands = ImmutableList.Create(Band("p", "home"), Band("w", "winget", showTitles: false)),
        };

        // Distinct ImmutableList instances — would be reference-unequal without EquatableList.
        Assert.AreNotSame(a.StartBands, b.StartBands);
        Assert.AreEqual(a, b);
        Assert.IsTrue(a == b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void NotEqual_WhenABandDiffers()
    {
        var a = new DockSettings { StartBands = ImmutableList.Create(Band("p", "home")) };
        var b = new DockSettings { StartBands = ImmutableList.Create(Band("p", "settings")) };

        Assert.AreNotEqual(a, b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void NotEqual_WhenBandOrderDiffers()
    {
        var a = new DockSettings { StartBands = ImmutableList.Create(Band("p", "a"), Band("p", "b")) };
        var b = new DockSettings { StartBands = ImmutableList.Create(Band("p", "b"), Band("p", "a")) };

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equal_WhenMonitorConfigsHaveSameContentIncludingPerMonitorBands()
    {
        var a = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(new DockMonitorConfig
            {
                MonitorDeviceId = @"\\.\DISPLAY1",
                IsCustomized = true,
                StartBands = ImmutableList.Create(Band("p", "home")),
            }),
        };
        var b = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(new DockMonitorConfig
            {
                MonitorDeviceId = @"\\.\DISPLAY1",
                IsCustomized = true,
                StartBands = ImmutableList.Create(Band("p", "home")),
            }),
        };

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void NotEqual_WhenPerMonitorBandsIsNullVersusEmpty()
    {
        // null means "inherit global bands"; an explicit empty list does not. The two must
        // stay distinguishable so a monitor can't silently switch between the two meanings.
        var inherit = new DockMonitorConfig { MonitorDeviceId = "m", StartBands = null };
        var explicitEmpty = new DockMonitorConfig { MonitorDeviceId = "m", StartBands = ImmutableList<DockBandSettings>.Empty };

        Assert.AreNotEqual(inherit, explicitEmpty);
    }
}
