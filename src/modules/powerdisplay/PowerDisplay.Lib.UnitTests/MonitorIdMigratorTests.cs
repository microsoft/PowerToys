// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorIdMigratorTests
{
    private const string DellDevicePathA = @"\\?\DISPLAY#DELD1A8#5&abc&0&UID12345";
    private const string DellDevicePathB = @"\\?\DISPLAY#DELD1A8#5&def&0&UID67890";
    private const string BoeDevicePath = @"\\?\DISPLAY#BOE0900#4&xyz&0&UID111";

    [TestMethod]
    public void MatchNewId_SingleMatch_ReturnsNewId()
    {
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_DELD1A8_1",
            new[] { (DellDevicePathA, 1) });

        Assert.AreEqual(DellDevicePathA, result);
    }

    [TestMethod]
    public void MatchNewId_NoMatchingEdid_ReturnsNull()
    {
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_HPS2719_1",
            new[] { (DellDevicePathA, 1) });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_WmiPrefixIsRecognized()
    {
        var result = MonitorIdMigrator.MatchNewId(
            "WMI_BOE0900_1",
            new[] { (BoeDevicePath, 1) });

        Assert.AreEqual(BoeDevicePath, result);
    }

    [TestMethod]
    public void MatchNewId_UnknownPlaceholder_ReturnsNull()
    {
        // "Unknown" was the placeholder PowerDisplay wrote when EDID was unavailable —
        // it can never identify a specific monitor, so the migrator must skip it.
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_Unknown_1",
            new[] { (DellDevicePathA, 1) });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_NewFormatInput_ReturnsNull()
    {
        // A new-format Id is not a legacy entry; calling MatchNewId on it must not match.
        var result = MonitorIdMigrator.MatchNewId(
            DellDevicePathA,
            new[] { (DellDevicePathA, 1) });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(MonitorIdMigrator.MatchNewId(null, new[] { (DellDevicePathA, 1) }));
        Assert.IsNull(MonitorIdMigrator.MatchNewId(string.Empty, new[] { (DellDevicePathA, 1) }));
    }

    [TestMethod]
    public void MatchNewId_TwoLegacyEntries_AssignedDistinctlyByMonitorNumber()
    {
        // Real bug scenario: two physically identical monitors (same EdidId, different
        // Windows DISPLAY numbers). EdidId-only matching would attach both legacy entries
        // to the first new-format Id and lose one user's preferences. Strict
        // (EdidId, MonitorNumber) matching keeps them distinct.
        var discovered = new[] { (DellDevicePathA, 1), (DellDevicePathB, 2) };

        Assert.AreEqual(DellDevicePathA, MonitorIdMigrator.MatchNewId("DDC_DELD1A8_1", discovered));
        Assert.AreEqual(DellDevicePathB, MonitorIdMigrator.MatchNewId("DDC_DELD1A8_2", discovered));
    }

    [TestMethod]
    public void MatchNewId_EdidMatchesButMonitorNumberDiffers_ReturnsNull()
    {
        // User swapped a monitor to a different port; EdidId still matches but
        // Windows assigned a different DISPLAY number. We refuse to guess — better
        // to drop the legacy entry than risk attaching preferences to the wrong screen
        // (e.g., enabling power-state toggle on a monitor without physical recovery).
        var discovered = new[] { (DellDevicePathA, 2) };

        Assert.IsNull(MonitorIdMigrator.MatchNewId("DDC_DELD1A8_1", discovered));
    }

    [TestMethod]
    public void MatchNewId_OneLegacyToOneOfTwoIdentical_AssignedByNumber()
    {
        // User originally had one DELD1A8 monitor; later added a second identical one.
        // The legacy entry's MonitorNumber pins down which of the two new entries to
        // attach to — without it the migration would be 50/50.
        var discovered = new[] { (DellDevicePathA, 1), (DellDevicePathB, 2) };

        Assert.AreEqual(DellDevicePathA, MonitorIdMigrator.MatchNewId("DDC_DELD1A8_1", discovered));
    }

    [TestMethod]
    public void MatchNewId_EmptyDiscoveredList_ReturnsNull()
    {
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_DELD1A8_1",
            System.Array.Empty<(string, int)>());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_EdidMatchIsCaseInsensitive()
    {
        // Defensive: EDID PnP ids are conventionally uppercase but we tolerate either casing.
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_deld1a8_1",
            new[] { (DellDevicePathA, 1) });

        Assert.AreEqual(DellDevicePathA, result);
    }

    [TestMethod]
    public void MatchNewId_MonitorNumberZero_ReturnsNull()
    {
        // Defensive: a legacy id with trailing "_0" cannot identify a specific
        // Windows DISPLAY number (which is 1-based), so we drop it rather than
        // accidentally matching a current monitor whose number is also 0.
        var discovered = new[] { (DellDevicePathA, 0) };

        Assert.IsNull(MonitorIdMigrator.MatchNewId("DDC_DELD1A8_0", discovered));
    }
}
