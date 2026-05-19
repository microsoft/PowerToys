// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorIdMigratorTests
{
    private const string DellDevicePathA = @"\\?\DISPLAY#DELD1A8#5&abc&0&UID12345";
    private const string DellDevicePathB = @"\\?\DISPLAY#DELD1A8#5&def&0&UID67890";
    private const string BoeDevicePath = @"\\?\DISPLAY#BOE0900#4&xyz&0&UID111";

    private static void Union(MonitorInfo target, MonitorInfo legacy)
    {
        target.IsHidden |= legacy.IsHidden;
        target.EnableContrast |= legacy.EnableContrast;
        target.EnableVolume |= legacy.EnableVolume;
        target.EnableInputSource |= legacy.EnableInputSource;
        target.EnableRotation |= legacy.EnableRotation;
        target.EnableColorTemperature |= legacy.EnableColorTemperature;
        target.EnablePowerState |= legacy.EnablePowerState;
    }

    [TestMethod]
    public void MergeLegacyPreferences_SingleMatch_CopiesOptInsOntoNewEntry()
    {
        var discovered = new MonitorInfo { Id = DellDevicePathA };
        var legacy = new MonitorInfo
        {
            Id = "DDC_DELD1A8_1",
            EnableInputSource = true,
            EnableColorTemperature = true,
        };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            new List<MonitorInfo> { legacy },
            Union);

        Assert.IsTrue(discovered.EnableInputSource);
        Assert.IsTrue(discovered.EnableColorTemperature);
        Assert.AreEqual(1, consumed.Count);
        Assert.AreSame(legacy, consumed[0]);
    }

    [TestMethod]
    public void MergeLegacyPreferences_NoMatchingEdid_DoesNothingAndConsumesNothing()
    {
        var discovered = new MonitorInfo { Id = DellDevicePathA };
        var legacy = new MonitorInfo
        {
            Id = "DDC_HPS2719_1",
            EnableInputSource = true,
        };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            new List<MonitorInfo> { legacy },
            Union);

        Assert.IsFalse(discovered.EnableInputSource);
        Assert.AreEqual(0, consumed.Count);
    }

    [TestMethod]
    public void MergeLegacyPreferences_NewFormatEntriesAreIgnored()
    {
        // Already-migrated entries on disk must not be re-processed as if they were legacy.
        var discovered = new MonitorInfo { Id = DellDevicePathA };
        var alreadyNewFormat = new MonitorInfo
        {
            Id = DellDevicePathA,
            EnableInputSource = true,
        };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            new List<MonitorInfo> { alreadyNewFormat },
            Union);

        Assert.AreEqual(0, consumed.Count);
        Assert.IsFalse(discovered.EnableInputSource);
    }

    [TestMethod]
    public void MergeLegacyPreferences_UnknownPlaceholderEdidIsSkipped()
    {
        var discovered = new MonitorInfo { Id = DellDevicePathA };
        var legacy = new MonitorInfo
        {
            Id = "DDC_Unknown_1",
            EnableInputSource = true,
        };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            new List<MonitorInfo> { legacy },
            Union);

        Assert.AreEqual(0, consumed.Count);
        Assert.IsFalse(discovered.EnableInputSource);
    }

    [TestMethod]
    public void MergeLegacyPreferences_IdenticalMonitors_BroadcastsUnionToBoth()
    {
        // User has two DELD1A8 monitors and customized at least one with EnableInputSource.
        // After migration, both currently-discovered monitors should retain the opt-in —
        // re-disabling is one click in Settings UI; silently losing the opt-in is invisible.
        var discoveredA = new MonitorInfo { Id = DellDevicePathA };
        var discoveredB = new MonitorInfo { Id = DellDevicePathB };
        var legacy1 = new MonitorInfo { Id = "DDC_DELD1A8_1", EnableInputSource = true };
        var legacy2 = new MonitorInfo { Id = "DDC_DELD1A8_2", EnableColorTemperature = true };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discoveredA, discoveredB },
            new List<MonitorInfo> { legacy1, legacy2 },
            Union);

        Assert.IsTrue(discoveredA.EnableInputSource);
        Assert.IsTrue(discoveredA.EnableColorTemperature);
        Assert.IsTrue(discoveredB.EnableInputSource);
        Assert.IsTrue(discoveredB.EnableColorTemperature);
        Assert.AreEqual(2, consumed.Count);
    }

    [TestMethod]
    public void MergeLegacyPreferences_UnionDoesNotClearAlreadySetFlags()
    {
        // Currently-discovered monitor already has a flag set (e.g. from
        // ApplyPreservedUserSettings finding a matching new-format entry). The migrator
        // must never overwrite true → false.
        var discovered = new MonitorInfo { Id = DellDevicePathA, EnableInputSource = true };
        var legacy = new MonitorInfo { Id = "DDC_DELD1A8_1", EnableInputSource = false };

        MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            new List<MonitorInfo> { legacy },
            Union);

        Assert.IsTrue(discovered.EnableInputSource);
    }

    [TestMethod]
    public void MergeLegacyPreferences_IsHiddenIsPreservedThroughMigration()
    {
        var discovered = new MonitorInfo { Id = DellDevicePathA };
        var legacy = new MonitorInfo { Id = "DDC_DELD1A8_1", IsHidden = true };

        MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            new List<MonitorInfo> { legacy },
            Union);

        Assert.IsTrue(discovered.IsHidden);
    }

    [TestMethod]
    public void MergeLegacyPreferences_BothDdcAndWmiPrefixesAreRecognized()
    {
        var discoveredBoe = new MonitorInfo { Id = BoeDevicePath };
        var wmiLegacy = new MonitorInfo
        {
            Id = "WMI_BOE0900_1",
            EnableContrast = true,
        };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discoveredBoe },
            new List<MonitorInfo> { wmiLegacy },
            Union);

        Assert.IsTrue(discoveredBoe.EnableContrast);
        Assert.AreEqual(1, consumed.Count);
    }

    [TestMethod]
    public void MergeLegacyPreferences_NoCurrentlyDiscovered_NothingHappens()
    {
        var legacy = new MonitorInfo { Id = "DDC_DELD1A8_1", EnableInputSource = true };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo>(),
            new List<MonitorInfo> { legacy },
            Union);

        Assert.AreEqual(0, consumed.Count);
        Assert.IsTrue(legacy.EnableInputSource); // unchanged
    }

    [TestMethod]
    public void MergeLegacyPreferences_ConsumedEntries_CanBeFilteredFromRetentionInput()
    {
        // End-to-end shape check: the caller subtracts consumed legacy entries from the
        // rebuilder input so they don't linger for 30 days under stale Ids.
        var discovered = new MonitorInfo { Id = DellDevicePathA };
        var legacy = new MonitorInfo { Id = "DDC_DELD1A8_1", EnableInputSource = true };
        var unrelatedLegacy = new MonitorInfo { Id = "DDC_HPS2719_1", EnableInputSource = true };
        var existing = new List<MonitorInfo> { legacy, unrelatedLegacy };

        var consumed = MonitorIdMigrator.MergeLegacyPreferences(
            new List<MonitorInfo> { discovered },
            existing,
            Union);

        var retentionInput = existing.Except(consumed).ToList();

        Assert.AreEqual(1, retentionInput.Count);
        Assert.AreEqual("DDC_HPS2719_1", retentionInput[0].Id);
    }
}
