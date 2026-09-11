// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers replacement and isolation of the settings snapshot consulted by DDC writes.
/// Managers are never asked to discover monitors or perform hardware operations.
/// </summary>
[TestClass]
public sealed class MonitorManagerVcpValueBlockTests
{
    private const string AffectedMonitorId = @"\\?\DISPLAY#SAM105C#5&ABC&0&UID1";
    private const string OtherMonitorId = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID2";

    [TestMethod]
    public void UserRules_MatchMonitorIdsIgnoringCaseButKeepInstancesAndCodesSeparate()
    {
        using var manager = new MonitorManager(new RecordingKnownGoodStore());
        manager.SetDisabledVcpValues(new Dictionary<string, List<VcpValueBlock>>
        {
            [MonitorId] = new() { new() { VcpCode = 0xD6, Values = new() { 0x05 } } },
            [OtherMonitorId] = new() { new() { VcpCode = 0x60, Values = new() { 0x1234 } } },
        });

        Assert.IsTrue(manager.IsVcpValueBlocked(MonitorId.ToLowerInvariant(), 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(OtherMonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0x14, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x01));
        Assert.IsTrue(manager.IsVcpValueBlocked(OtherMonitorId.ToLowerInvariant(), 0x60, 0x1234));
        Assert.IsFalse(manager.IsVcpValueBlocked(OtherMonitorId, 0x60, 0x34));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0x60, 0x1234));
    }

    [TestMethod]
    public void SetDisabledVcpValues_DeepCopiesEveryMutableLevel()
    {
        using var manager = new MonitorManager(new RecordingKnownGoodStore());
        var values = new List<int> { 0x05 };
        var block = new VcpValueBlock { VcpCode = 0xD6, Values = values };
        var blocks = new List<VcpValueBlock> { block };
        var entries = new Dictionary<string, List<VcpValueBlock>> { [MonitorId] = blocks };

        manager.SetDisabledVcpValues(entries);

        block.VcpCode = 0x60;
        values.Clear();
        values.Add(0x04);
        blocks.Clear();
        blocks.Add(new VcpValueBlock { VcpCode = 0x12, Values = new() { 30 } });
        entries.Clear();

        Assert.IsTrue(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x04));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0x60, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0x60, 0x04));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0x12, 30));
    }

    [TestMethod]
    public void SetDisabledVcpValues_ReplacesAndClearsThePreviousSnapshot()
    {
        using var manager = new MonitorManager(new RecordingKnownGoodStore());
        manager.SetDisabledVcpValues(new Dictionary<string, List<VcpValueBlock>>
        {
            [MonitorId] = new() { new() { VcpCode = 0xD6, Values = new() { 0x05 } } },
        });
        Assert.IsTrue(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x05));

        manager.SetDisabledVcpValues(new Dictionary<string, List<VcpValueBlock>>
        {
            [OtherMonitorId] = new() { new() { VcpCode = 0x60, Values = new() { 0x11 } } },
        });
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x05));
        Assert.IsTrue(manager.IsVcpValueBlocked(OtherMonitorId, 0x60, 0x11));

        manager.SetDisabledVcpValues(Array.Empty<KeyValuePair<string, List<VcpValueBlock>>>());
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(OtherMonitorId, 0x60, 0x11));
    }

    [TestMethod]
    public void HardwareRules_CannotBeOverriddenByEmptyOrReplacedUserSettings()
    {
        using var manager = new MonitorManager(new RecordingKnownGoodStore());
        Assert.IsTrue(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x05));

        manager.SetDisabledVcpValues(new Dictionary<string, List<VcpValueBlock>>
        {
            [AffectedMonitorId.ToLowerInvariant()] = new() { new() { VcpCode = 0xD6, Values = new() { 0x04 } } },
        });
        Assert.IsTrue(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x04));
        Assert.IsTrue(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x01));

        manager.SetDisabledVcpValues(new Dictionary<string, List<VcpValueBlock>> { [AffectedMonitorId] = new() });
        Assert.IsFalse(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x04));
        Assert.IsTrue(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x05));

        manager.SetDisabledVcpValues(Array.Empty<KeyValuePair<string, List<VcpValueBlock>>>());
        Assert.IsTrue(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(AffectedMonitorId, 0x14, 0x05));
    }

    [TestMethod]
    public void SetDisabledVcpValues_IgnoresEmptyIdsAndNullRules()
    {
        using var manager = new MonitorManager(new RecordingKnownGoodStore());
        manager.SetDisabledVcpValues(new List<KeyValuePair<string, List<VcpValueBlock>>>
        {
            new(null!, new() { new() { VcpCode = 0xD6, Values = new() { 0x05 } } }),
            new(string.Empty, new() { new() { VcpCode = 0xD6, Values = new() { 0x05 } } }),
            new(MonitorId, null!),
            new(OtherMonitorId, new() { null!, new() { VcpCode = 0xD6, Values = null! } }),
        });

        Assert.IsFalse(manager.IsVcpValueBlocked(string.Empty, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(MonitorId, 0xD6, 0x05));
        Assert.IsFalse(manager.IsVcpValueBlocked(OtherMonitorId, 0xD6, 0x05));
        Assert.IsTrue(manager.IsVcpValueBlocked(AffectedMonitorId, 0xD6, 0x05));
    }
}
