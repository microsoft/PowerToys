// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class ProfileMigrationTests
{
    private const string NewMonitorId = @"\\?\DISPLAY#DELD1A8#5&abc&0&UID12345";

    [TestMethod]
    public void Migrate_NoDiscoveredMonitors_BackfillsIdsWithoutChangingMonitorSettings()
    {
        var profile = MakeProfile("Legacy", "DDC_DELD1A8_1");
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(profile);

        var changed = ProfileMigration.Migrate(
            profiles,
            System.Array.Empty<(string Id, int MonitorNumber)>());

        Assert.IsTrue(changed);
        Assert.AreEqual(1, profile.Id);
        Assert.AreEqual("DDC_DELD1A8_1", profile.MonitorSettings[0].MonitorId);
        Assert.AreEqual(2, profiles.NextId);
    }

    [TestMethod]
    public void Migrate_DiscoveredMonitor_BackfillsIdAndMigratesMonitorReference()
    {
        var profile = MakeProfile("Legacy", "DDC_DELD1A8_1");
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(profile);

        var changed = ProfileMigration.Migrate(
            profiles,
            new[] { (NewMonitorId, 1) });

        Assert.IsTrue(changed);
        Assert.AreEqual(1, profile.Id);
        Assert.AreEqual(1, profile.MonitorSettings.Count);
        Assert.AreEqual(NewMonitorId, profile.MonitorSettings[0].MonitorId);
    }

    [TestMethod]
    public void Migrate_DiscoveredMonitor_DeduplicatesWhenNewSettingAlreadyExists()
    {
        var profile = new PowerDisplayProfile(
            "Legacy",
            new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("DDC_DELD1A8_1", 50, null, null, null),
                new ProfileMonitorSetting(NewMonitorId, 50, null, null, null),
            });
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(profile);

        var changed = ProfileMigration.Migrate(
            profiles,
            new[] { (NewMonitorId, 1) });

        Assert.IsTrue(changed);
        Assert.AreEqual(1, profile.MonitorSettings.Count);
        Assert.AreEqual(NewMonitorId, profile.MonitorSettings[0].MonitorId);
    }

    private static PowerDisplayProfile MakeProfile(string name, string monitorId)
    {
        return new PowerDisplayProfile(
            name,
            new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting(monitorId, 50, null, null, null),
            });
    }
}
