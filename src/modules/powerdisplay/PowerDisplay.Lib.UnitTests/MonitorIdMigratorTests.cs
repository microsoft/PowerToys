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
        var result = MonitorIdMigrator.MatchNewId("DDC_DELD1A8_1", new[] { DellDevicePathA });

        Assert.AreEqual(DellDevicePathA, result);
    }

    [TestMethod]
    public void MatchNewId_NoMatchingEdid_ReturnsNull()
    {
        var result = MonitorIdMigrator.MatchNewId("DDC_HPS2719_1", new[] { DellDevicePathA });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_WmiPrefixIsRecognized()
    {
        var result = MonitorIdMigrator.MatchNewId("WMI_BOE0900_1", new[] { BoeDevicePath });

        Assert.AreEqual(BoeDevicePath, result);
    }

    [TestMethod]
    public void MatchNewId_UnknownPlaceholder_ReturnsNull()
    {
        // "Unknown" was the placeholder PowerDisplay wrote when EDID was unavailable —
        // it can never identify a specific monitor, so the migrator must skip it.
        var result = MonitorIdMigrator.MatchNewId("DDC_Unknown_1", new[] { DellDevicePathA });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_NewFormatInput_ReturnsNull()
    {
        // A new-format Id is not a legacy entry; calling MatchNewId on it must not match.
        var result = MonitorIdMigrator.MatchNewId(DellDevicePathA, new[] { DellDevicePathA });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(MonitorIdMigrator.MatchNewId(null, new[] { DellDevicePathA }));
        Assert.IsNull(MonitorIdMigrator.MatchNewId(string.Empty, new[] { DellDevicePathA }));
    }

    [TestMethod]
    public void MatchNewId_MultipleIdenticalMonitors_ReturnsFirst()
    {
        // First match wins; users with two physically identical monitors may need to
        // re-toggle one Enable* checkbox after the upgrade. Documented trade-off for
        // keeping the migration code minimal.
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_DELD1A8_1",
            new[] { DellDevicePathA, DellDevicePathB });

        Assert.AreEqual(DellDevicePathA, result);
    }

    [TestMethod]
    public void MatchNewId_EmptyDiscoveredList_ReturnsNull()
    {
        var result = MonitorIdMigrator.MatchNewId("DDC_DELD1A8_1", System.Array.Empty<string>());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchNewId_EdidMatchIsCaseInsensitive()
    {
        // Defensive: EDID PnP ids are conventionally uppercase but we tolerate either casing.
        var result = MonitorIdMigrator.MatchNewId(
            "DDC_deld1a8_1",
            new[] { DellDevicePathA });

        Assert.AreEqual(DellDevicePathA, result);
    }
}
