// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerDisplay.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class PowerDisplayInventoryTests
{
    private static readonly string[] LiveMonitorIds = ["live-a", "live-b"];

    [TestMethod]
    public void CurrentBatchRetainsEveryLiveMonitorIndependentlyOfFlyoutVisibility()
    {
        using var settings = JsonDocument.Parse("""
            [
              {"id":"live-a","isHidden":false,"lastSeenUtc":"2026-11-04T10:00:00Z"},
              {"id":"live-b","isHidden":false,"lastSeenUtc":"2026-11-04T10:00:00Z"},
              {"id":"retained","isHidden":false,"lastSeenUtc":"2026-11-03T10:00:00Z"},
              {"id":"hidden","isHidden":true,"lastSeenUtc":"2026-11-04T10:00:00Z"}
            ]
            """);

        var current = PowerDisplayInventory.CurrentBatch(settings.RootElement.EnumerateArray().ToArray());

        CollectionAssert.AreEquivalent(
            LiveMonitorIds,
            current.Select(monitor => monitor.GetProperty("id").GetString()).ToArray());
    }

    [TestMethod]
    public void HiddenCurrentBatchDoesNotPromoteAnOlderRetainedMonitor()
    {
        using var settings = JsonDocument.Parse("""
            [
              {"id":"retained","isHidden":false,"lastSeenUtc":"2026-11-03T10:00:00Z"},
              {"id":"hidden","isHidden":true,"lastSeenUtc":"2026-11-04T10:00:00Z"}
            ]
            """);

        Assert.IsEmpty(PowerDisplayInventory.CurrentBatch(settings.RootElement.EnumerateArray().ToArray()));
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("""[{"id":"legacy","isHidden":false,"lastSeenUtc":null}]""")]
    [DataRow("""[{"id":"legacy","isHidden":false}]""")]
    public void EmptyAndUnstampedInventoriesAreNotLive(string json)
    {
        using var settings = JsonDocument.Parse(json);

        Assert.IsEmpty(PowerDisplayInventory.CurrentBatch(settings.RootElement.EnumerateArray().ToArray()));
    }
}
