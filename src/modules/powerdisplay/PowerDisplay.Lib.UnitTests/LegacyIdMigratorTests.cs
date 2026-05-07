// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LegacyIdMigratorTests
{
    [TestMethod]
    public void TryParseLegacyId_ValidDdcId_ParsesEdidAndNumber()
    {
        bool ok = LegacyIdMigrator.TryParseLegacyId(
            "DDC_DELD1A8_1", out var edidId, out var monitorNumber);

        Assert.IsTrue(ok);
        Assert.AreEqual("DELD1A8", edidId);
        Assert.AreEqual(1, monitorNumber);
    }

    [TestMethod]
    public void TryParseLegacyId_ValidWmiId_ParsesEdidAndNumber()
    {
        bool ok = LegacyIdMigrator.TryParseLegacyId(
            "WMI_BOE0900_2", out var edidId, out var monitorNumber);

        Assert.IsTrue(ok);
        Assert.AreEqual("BOE0900", edidId);
        Assert.AreEqual(2, monitorNumber);
    }

    [TestMethod]
    public void TryParseLegacyId_NewFormatId_ReturnsFalse()
    {
        bool ok = LegacyIdMigrator.TryParseLegacyId(
            @"\\?\DISPLAY#DELD1A8#5&abc&0&UID1", out _, out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void TryParseLegacyId_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(LegacyIdMigrator.TryParseLegacyId(null, out _, out _));
        Assert.IsFalse(LegacyIdMigrator.TryParseLegacyId(string.Empty, out _, out _));
    }

    [TestMethod]
    public void TryMapLegacyId_LookupHit_ReturnsNewId()
    {
        var lookup = new Dictionary<(string EdidId, int MonitorNumber), string>
        {
            { ("DELD1A8", 1), @"\\?\DISPLAY#DELD1A8#5&abc&0&UID1" },
        };

        bool ok = LegacyIdMigrator.TryMapLegacyId("DDC_DELD1A8_1", lookup, out var newId);

        Assert.IsTrue(ok);
        Assert.AreEqual(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID1", newId);
    }

    [TestMethod]
    public void TryMapLegacyId_LookupMiss_ReturnsFalseAndKeepsOldId()
    {
        var lookup = new Dictionary<(string EdidId, int MonitorNumber), string>();

        bool ok = LegacyIdMigrator.TryMapLegacyId("DDC_DELD1A8_1", lookup, out var newId);

        Assert.IsFalse(ok);
        Assert.AreEqual("DDC_DELD1A8_1", newId);
    }

    [TestMethod]
    public void TryMapLegacyId_AlreadyNewFormat_ReturnsFalse()
    {
        var lookup = new Dictionary<(string EdidId, int MonitorNumber), string>
        {
            { ("DELD1A8", 1), "irrelevant" },
        };

        bool ok = LegacyIdMigrator.TryMapLegacyId(
            @"\\?\DISPLAY#DELD1A8#5&abc&0&UID1", lookup, out var newId);

        Assert.IsFalse(ok);
        Assert.AreEqual(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID1", newId);
    }

    [TestMethod]
    public void BuildLookup_FromCurrentMonitors_KeysOnEdidIdAndMonitorNumber()
    {
        var current = new List<PowerDisplay.Common.Models.Monitor>
        {
            new() { Id = @"\\?\DISPLAY#DELD1A8#5&abc&0&UID1", MonitorNumber = 1 },
            new() { Id = @"\\?\DISPLAY#DELD1A8#5&xyz&0&UID2", MonitorNumber = 2 },
            new() { Id = "DDC_LEGACY_3", MonitorNumber = 3 }, // legacy-format entry — skipped
        };

        var lookup = LegacyIdMigrator.BuildLookup(current);

        Assert.AreEqual(2, lookup.Count);
        Assert.AreEqual(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID1", lookup[("DELD1A8", 1)]);
        Assert.AreEqual(@"\\?\DISPLAY#DELD1A8#5&xyz&0&UID2", lookup[("DELD1A8", 2)]);
    }
}
