// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers the persisted shape of the linked-brightness feature on
/// <see cref="PowerDisplayProperties"/>: defaults, JSON property names, and — most importantly —
/// that settings written before the feature existed deserialize to safe defaults without any
/// migration step (the forward-compatibility promise made to the module owner).
/// </summary>
[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class LinkedBrightnessSettingsTests
{
    [TestMethod]
    public void Defaults_LinkDisabled_AndExclusionListEmptyButNotNull()
    {
        var properties = new PowerDisplayProperties();

        Assert.IsFalse(properties.LinkedLevelsActive, "Linked brightness must default to off.");
        Assert.IsNotNull(properties.ExcludedFromSyncMonitorIds, "Exclusion list must never be null.");
        Assert.IsEmpty(properties.ExcludedFromSyncMonitorIds, "Exclusion list must start empty.");
    }

    [TestMethod]
    public void Deserialize_LegacyJsonMissingLinkFields_UsesDefaultsWithoutMigration()
    {
        // A settings.json captured before the linked-brightness feature shipped: it has none of
        // the new keys. Deserializing must fall back to the constructor defaults rather than
        // produce nulls or throw — this is the "no migration needed" guarantee.
        const string legacyJson = """
        {
            "monitor_refresh_delay": 5,
            "restore_settings_on_startup": false,
            "show_system_tray_icon": true
        }
        """;

        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(legacyJson);

        Assert.IsNotNull(properties);
        Assert.IsFalse(properties.LinkedLevelsActive);
        Assert.IsNotNull(properties.ExcludedFromSyncMonitorIds);
        Assert.IsEmpty(properties.ExcludedFromSyncMonitorIds);
    }

    [TestMethod]
    public void RoundTrip_PreservesLinkStateAndExclusionList()
    {
        var original = new PowerDisplayProperties
        {
            LinkedLevelsActive = true,
        };
        original.ExcludedFromSyncMonitorIds.Add(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID4357");
        original.ExcludedFromSyncMonitorIds.Add(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID4358");

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.LinkedLevelsActive);
        Assert.AreSequenceEqual(original.ExcludedFromSyncMonitorIds, restored.ExcludedFromSyncMonitorIds);
    }

    [TestMethod]
    public void Serialize_UsesSnakeCaseJsonKeys()
    {
        var properties = new PowerDisplayProperties { LinkedLevelsActive = true };
        properties.ExcludedFromSyncMonitorIds.Add("monitor-id");

        var json = JsonSerializer.Serialize(properties);

        Assert.Contains("\"linked_levels_active\":true", json);
        Assert.Contains("\"excluded_from_sync_monitor_ids\"", json);
    }

    [TestMethod]
    public void ExclusionList_DistinguishesIdenticalModelMonitorsByDevicePath()
    {
        // Two physically identical monitors share an EdidId (DELD1A8) but differ in the PnP UID
        // segment of Monitor.Id. Keying the exclusion set by Monitor.Id keeps them distinct, which
        // is the whole reason the issue's "three identical monitors" scenario works.
        var properties = new PowerDisplayProperties();
        properties.ExcludedFromSyncMonitorIds.Add(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID4357");

        Assert.Contains(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID4357", properties.ExcludedFromSyncMonitorIds);
        Assert.DoesNotContain(
            @"\\?\DISPLAY#DELD1A8#5&abc&0&UID4358",
            properties.ExcludedFromSyncMonitorIds,
            "A different physical port (UID) must not be treated as excluded.");
    }
}
