// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

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

    private static PowerDisplayProfiles ProfilesWith(params PowerDisplayProfile[] profiles)
    {
        var bag = new PowerDisplayProfiles();
        foreach (var p in profiles)
        {
            bag.Profiles.Add(p);
        }

        return bag;
    }

    private static PowerDisplayProfile ProfileWith(string name, params ProfileMonitorSetting[] settings)
        => new(name, settings.ToList());

    [TestMethod]
    public void MigrateProfileMonitorIds_NoLegacyEntries_ReturnsFalse()
    {
        var profiles = ProfilesWith(
            ProfileWith("p1", new ProfileMonitorSetting(DellDevicePathA, brightness: 50)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA });

        Assert.IsFalse(changed);
        Assert.AreEqual(DellDevicePathA, profiles.Profiles[0].MonitorSettings[0].MonitorId);
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_SingleMatch_RewritesIdInPlace()
    {
        var profiles = ProfilesWith(
            ProfileWith("p1", new ProfileMonitorSetting("DDC_DELD1A8_1", brightness: 70, contrast: 60)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA });

        Assert.IsTrue(changed);
        var entries = profiles.Profiles[0].MonitorSettings;
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(DellDevicePathA, entries[0].MonitorId);
        Assert.AreEqual(70, entries[0].Brightness);
        Assert.AreEqual(60, entries[0].Contrast);
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_IdenticalMonitors_FansOutOneEntryIntoTwo()
    {
        // User has two DELD1A8 monitors. A pre-#47712 profile only had a single
        // "DDC_DELD1A8_1" entry; on upgrade we want both physical monitors to be
        // driven from that profile to the same target values.
        var profiles = ProfilesWith(
            ProfileWith("p1", new ProfileMonitorSetting("DDC_DELD1A8_1", brightness: 80)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA, DellDevicePathB });

        Assert.IsTrue(changed);
        var entries = profiles.Profiles[0].MonitorSettings;
        Assert.AreEqual(2, entries.Count);
        Assert.IsTrue(entries.Any(e => e.MonitorId == DellDevicePathA && e.Brightness == 80));
        Assert.IsTrue(entries.Any(e => e.MonitorId == DellDevicePathB && e.Brightness == 80));
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_AlreadyMigratedNewIdEntry_IsNotOverwritten()
    {
        // Profile already has a new-format entry for the same monitor (user re-saved
        // post-upgrade). The legacy entry should be removed without clobbering the
        // fresher new-format brightness target.
        var profiles = ProfilesWith(
            ProfileWith(
                "p1",
                new ProfileMonitorSetting(DellDevicePathA, brightness: 30),
                new ProfileMonitorSetting("DDC_DELD1A8_1", brightness: 80)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA });

        Assert.IsTrue(changed);
        var entries = profiles.Profiles[0].MonitorSettings;
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(DellDevicePathA, entries[0].MonitorId);
        Assert.AreEqual(30, entries[0].Brightness); // newer value wins
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_NoMatchingEdid_LeavesEntryAlone()
    {
        // Monitor that's currently disconnected: profile entry stays under its legacy Id,
        // so the next time the monitor is plugged in (which will discover it under a new
        // Id), the profile won't drive it. That's the same UX as today and acceptable —
        // the alternative is losing the entry entirely.
        var profiles = ProfilesWith(
            ProfileWith("p1", new ProfileMonitorSetting("DDC_HPS2719_1", brightness: 40)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA });

        Assert.IsFalse(changed);
        Assert.AreEqual("DDC_HPS2719_1", profiles.Profiles[0].MonitorSettings[0].MonitorId);
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_UnknownPlaceholderIsSkipped()
    {
        var profiles = ProfilesWith(
            ProfileWith("p1", new ProfileMonitorSetting("DDC_Unknown_1", brightness: 50)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA });

        Assert.IsFalse(changed);
        Assert.AreEqual("DDC_Unknown_1", profiles.Profiles[0].MonitorSettings[0].MonitorId);
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_MultipleProfiles_AllMigratedIndependently()
    {
        var profiles = ProfilesWith(
            ProfileWith("day", new ProfileMonitorSetting("DDC_DELD1A8_1", brightness: 100)),
            ProfileWith("night", new ProfileMonitorSetting("DDC_DELD1A8_1", brightness: 20)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            new[] { DellDevicePathA });

        Assert.IsTrue(changed);
        Assert.AreEqual(DellDevicePathA, profiles.Profiles[0].MonitorSettings[0].MonitorId);
        Assert.AreEqual(DellDevicePathA, profiles.Profiles[1].MonitorSettings[0].MonitorId);
        Assert.AreEqual(100, profiles.Profiles[0].MonitorSettings[0].Brightness);
        Assert.AreEqual(20, profiles.Profiles[1].MonitorSettings[0].Brightness);
    }

    [TestMethod]
    public void MigrateProfileMonitorIds_EmptyDiscoveredList_NoOp()
    {
        var profiles = ProfilesWith(
            ProfileWith("p1", new ProfileMonitorSetting("DDC_DELD1A8_1", brightness: 70)));

        var changed = MonitorIdMigrator.MigrateProfileMonitorIds(
            profiles,
            System.Array.Empty<string>());

        Assert.IsFalse(changed);
    }

    private static MonitorStateEntry State(int brightness)
        => new() { Brightness = brightness };

    private static MonitorStateEntry Clone(MonitorStateEntry s)
        => new()
        {
            Brightness = s.Brightness,
            ColorTemperatureVcp = s.ColorTemperatureVcp,
            Contrast = s.Contrast,
            Volume = s.Volume,
            CapabilitiesRaw = s.CapabilitiesRaw,
            LastUpdated = s.LastUpdated,
        };

    [TestMethod]
    public void MigrateStateKeys_SingleMatch_RenamesKey()
    {
        var states = new Dictionary<string, MonitorStateEntry>
        {
            ["DDC_DELD1A8_1"] = State(70),
        };

        var changed = MonitorIdMigrator.MigrateStateKeys(states, new[] { DellDevicePathA }, Clone);

        Assert.IsTrue(changed);
        Assert.AreEqual(1, states.Count);
        Assert.IsTrue(states.ContainsKey(DellDevicePathA));
        Assert.IsFalse(states.ContainsKey("DDC_DELD1A8_1"));
        Assert.AreEqual(70, states[DellDevicePathA].Brightness);
    }

    [TestMethod]
    public void MigrateStateKeys_IdenticalMonitors_ClonesValueOntoBothKeys()
    {
        var states = new Dictionary<string, MonitorStateEntry>
        {
            ["DDC_DELD1A8_1"] = State(80),
        };

        var changed = MonitorIdMigrator.MigrateStateKeys(
            states,
            new[] { DellDevicePathA, DellDevicePathB },
            Clone);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, states.Count);
        Assert.AreEqual(80, states[DellDevicePathA].Brightness);
        Assert.AreEqual(80, states[DellDevicePathB].Brightness);

        // Clones must not alias — mutating one must not affect the other.
        states[DellDevicePathA].Brightness = 5;
        Assert.AreEqual(80, states[DellDevicePathB].Brightness);
    }

    [TestMethod]
    public void MigrateStateKeys_NewKeyAlreadyExists_LegacyDoesNotOverwrite()
    {
        // A new-format entry has been written since the upgrade (fresh user activity).
        // The legacy snapshot must not clobber it.
        var states = new Dictionary<string, MonitorStateEntry>
        {
            [DellDevicePathA] = State(40),
            ["DDC_DELD1A8_1"] = State(80),
        };

        var changed = MonitorIdMigrator.MigrateStateKeys(states, new[] { DellDevicePathA }, Clone);

        Assert.IsTrue(changed);
        Assert.AreEqual(1, states.Count);
        Assert.AreEqual(40, states[DellDevicePathA].Brightness);
        Assert.IsFalse(states.ContainsKey("DDC_DELD1A8_1"));
    }

    [TestMethod]
    public void MigrateStateKeys_NoMatchingEdid_KeyIsLeftAlone()
    {
        // Currently-disconnected monitor: state entry stays under its legacy key so
        // future reconnect doesn't lose previously-known brightness/contrast.
        var states = new Dictionary<string, MonitorStateEntry>
        {
            ["DDC_HPS2719_1"] = State(40),
        };

        var changed = MonitorIdMigrator.MigrateStateKeys(states, new[] { DellDevicePathA }, Clone);

        Assert.IsFalse(changed);
        Assert.IsTrue(states.ContainsKey("DDC_HPS2719_1"));
    }

    [TestMethod]
    public void MigrateStateKeys_NoLegacyKeys_ReturnsFalseFast()
    {
        var states = new Dictionary<string, MonitorStateEntry>
        {
            [DellDevicePathA] = State(70),
        };

        var changed = MonitorIdMigrator.MigrateStateKeys(states, new[] { DellDevicePathA }, Clone);

        Assert.IsFalse(changed);
        Assert.AreEqual(70, states[DellDevicePathA].Brightness);
    }

    [TestMethod]
    public void MigrateStateKeys_UnknownPlaceholderIsSkipped()
    {
        var states = new Dictionary<string, MonitorStateEntry>
        {
            ["DDC_Unknown_1"] = State(50),
        };

        var changed = MonitorIdMigrator.MigrateStateKeys(states, new[] { DellDevicePathA }, Clone);

        Assert.IsFalse(changed);
        Assert.IsTrue(states.ContainsKey("DDC_Unknown_1"));
    }
}
