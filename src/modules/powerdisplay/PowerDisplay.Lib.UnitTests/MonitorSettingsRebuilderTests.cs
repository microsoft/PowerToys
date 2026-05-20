// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorSettingsRebuilderTests
{
    private sealed class FixedClock(DateTime now) : ISystemClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private static readonly DateTime Now = new(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

    private static MonitorInfo Existing(string id, bool enableInputSource, DateTime? lastSeen, bool isHidden = false)
        => new()
        {
            Id = id,
            EnableInputSource = enableInputSource,
            IsHidden = isHidden,
            LastSeenUtc = lastSeen,
        };

    [TestMethod]
    public void Rebuild_KeepsCurrentlyDiscoveredAndStampsTheirLastSeen()
    {
        var current = new List<MonitorInfo>
        {
            new() { Id = "new-id-1", EnableInputSource = true },
        };
        var existing = new List<MonitorInfo>();

        var result = MonitorSettingsRebuilder.Rebuild(current, existing, new FixedClock(Now), retentionDays: 30);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("new-id-1", result[0].Id);
        Assert.AreEqual(Now, result[0].LastSeenUtc);
    }

    [TestMethod]
    public void Rebuild_KeepsRecentlyMissingMonitorWithEnableFlagsIntact()
    {
        var current = new List<MonitorInfo>();
        var existing = new List<MonitorInfo> { Existing("missing-1", enableInputSource: true, Now.AddDays(-5)) };

        var result = MonitorSettingsRebuilder.Rebuild(current, existing, new FixedClock(Now), retentionDays: 30);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("missing-1", result[0].Id);
        Assert.IsTrue(result[0].EnableInputSource);
    }

    [TestMethod]
    public void Rebuild_DropsStaleMonitor()
    {
        var current = new List<MonitorInfo>();
        var existing = new List<MonitorInfo> { Existing("stale-1", enableInputSource: true, Now.AddDays(-31)) };

        var result = MonitorSettingsRebuilder.Rebuild(current, existing, new FixedClock(Now), retentionDays: 30);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Rebuild_HiddenMonitorPreservedRegardlessOfAge()
    {
        var current = new List<MonitorInfo>();
        var existing = new List<MonitorInfo>
        {
            Existing("hidden-1", enableInputSource: true, Now.AddDays(-100), isHidden: true),
        };

        var result = MonitorSettingsRebuilder.Rebuild(current, existing, new FixedClock(Now), retentionDays: 30);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("hidden-1", result[0].Id);
    }

    [TestMethod]
    public void Rebuild_NullLastSeen_StampsToNowAndKeepsEntry()
    {
        var current = new List<MonitorInfo>();
        var existing = new List<MonitorInfo> { Existing("upgraded-1", enableInputSource: true, lastSeen: null) };

        var result = MonitorSettingsRebuilder.Rebuild(current, existing, new FixedClock(Now), retentionDays: 30);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(Now, result[0].LastSeenUtc);
    }

    [TestMethod]
    public void Rebuild_DiscoveryRevocationRoundtrip_DoesNotLoseFlags()
    {
        // Step A: monitor present, flag set
        var monitor = new MonitorInfo { Id = "x", EnableInputSource = true };
        var afterA = MonitorSettingsRebuilder.Rebuild(
            new List<MonitorInfo> { monitor },
            new List<MonitorInfo>(),
            new FixedClock(Now),
            retentionDays: 30);

        // Step B: monitor disappears (transient discovery failure)
        var afterB = MonitorSettingsRebuilder.Rebuild(
            new List<MonitorInfo>(),
            afterA,
            new FixedClock(Now.AddSeconds(1)),
            retentionDays: 30);
        Assert.AreEqual(1, afterB.Count, "missing-but-recent entry must survive");
        Assert.IsTrue(afterB[0].EnableInputSource, "Enable* flags must survive across revocation");

        // Step C: monitor reappears
        var monitorAgain = new MonitorInfo { Id = "x", EnableInputSource = true };
        var afterC = MonitorSettingsRebuilder.Rebuild(
            new List<MonitorInfo> { monitorAgain },
            afterB,
            new FixedClock(Now.AddSeconds(2)),
            retentionDays: 30);

        Assert.AreEqual(1, afterC.Count);
        Assert.AreEqual("x", afterC[0].Id);
        Assert.IsTrue(afterC[0].EnableInputSource);
    }
}
