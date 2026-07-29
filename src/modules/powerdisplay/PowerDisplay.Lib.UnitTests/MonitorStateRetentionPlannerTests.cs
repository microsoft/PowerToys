// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class MonitorStateRetentionPlannerTests
{
    private const string ExistingMonitor = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1";
    private const string NewMonitor = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID2";

    [TestMethod]
    public void BuildDroppedIds_ReturnsEntriesRemovedFromRebuiltSettings()
    {
        var droppedIds = MonitorStateRetentionPlanner.BuildDroppedIds(
            new[] { ExistingMonitor, NewMonitor },
            new[] { NewMonitor });

        Assert.AreEqual(1, droppedIds.Count);
        Assert.IsTrue(droppedIds.Contains(ExistingMonitor));
    }

    [TestMethod]
    public void BuildDroppedIds_DefaultsSettingsSnapshotDropsNothing()
    {
        // A missing or corrupt settings.json makes GetSettingsOrDefault hand back a defaults object
        // with an empty monitor list. That is indistinguishable from a genuinely empty list, so it
        // must never authorise deleting the state of monitors that are simply not connected.
        var droppedIds = MonitorStateRetentionPlanner.BuildDroppedIds(
            Array.Empty<string>(),
            new[] { NewMonitor });

        Assert.AreEqual(0, droppedIds.Count);
    }

    [TestMethod]
    public void BuildDroppedIds_MatchesIdsCaseInsensitivelyAndIgnoresEmptyIds()
    {
        var droppedIds = MonitorStateRetentionPlanner.BuildDroppedIds(
            new[] { ExistingMonitor, string.Empty },
            new[] { ExistingMonitor.ToLowerInvariant() });

        Assert.AreEqual(0, droppedIds.Count);
    }
}
