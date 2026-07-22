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
    public void BuildRetainedIds_PreservesPreviousAndRebuiltSettingsEntries()
    {
        var retainedIds = MonitorStateRetentionPlanner.BuildRetainedIds(
            new[] { ExistingMonitor },
            new[] { NewMonitor });

        Assert.AreEqual(2, retainedIds.Count);
        Assert.IsTrue(retainedIds.Contains(ExistingMonitor));
        Assert.IsTrue(retainedIds.Contains(NewMonitor));
    }

    [TestMethod]
    public void BuildRetainedIds_RemovesEntryAfterItIsAbsentFromBothSnapshots()
    {
        var firstReconciliation = MonitorStateRetentionPlanner.BuildRetainedIds(
            new[] { ExistingMonitor },
            Array.Empty<string>());
        var nextReconciliation = MonitorStateRetentionPlanner.BuildRetainedIds(
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.IsTrue(firstReconciliation.Contains(ExistingMonitor));
        Assert.IsFalse(nextReconciliation.Contains(ExistingMonitor));
    }

    [TestMethod]
    public void BuildRetainedIds_DeduplicatesIdsCaseInsensitivelyAndIgnoresEmptyIds()
    {
        var retainedIds = MonitorStateRetentionPlanner.BuildRetainedIds(
            new[] { ExistingMonitor, string.Empty },
            new[] { ExistingMonitor.ToLowerInvariant() });

        Assert.AreEqual(1, retainedIds.Count);
        Assert.IsTrue(retainedIds.Contains(ExistingMonitor));
    }
}
