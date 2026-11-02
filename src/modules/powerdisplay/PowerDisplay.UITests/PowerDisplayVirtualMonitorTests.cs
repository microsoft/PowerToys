// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerDisplay.UITests;

public sealed partial class PowerDisplayEndToEndTests
{
    [TestMethod]
    [TestCategory("PowerDisplayVirtualMonitors")]
    public void VirtualMonitorsHaveDistinctStableIdentitiesAndIndependentBrightness()
    {
        var fixture = RequireFixture();
        var flyout = OpenFlyout();
        var ids = AssertFixtureInventory(flyout, 2);
        var original = fixture.Snapshot();
        try
        {
            foreach (var id in ids)
            {
                var connector = VirtualMonitorFixture.ConnectorForId(id);
                var target = connector == 0 ? 31 : 73;
                var before = fixture.Snapshot();
                Step($"Setting fixture monitor {connector} to {target} through its real UI slider");
                FindMonitorBrightnessSlider(flyout, id).SetValue(target);
                AssertFixtureBrightness(fixture, connector, target, before.Monitors[connector].Sets);
                var other = 1 - connector;
                Assert.AreEqual(before.Monitors[other].Brightness, fixture.Snapshot().Monitors[other].Brightness, "A per-monitor write changed the other display.");
                AssertSavedBrightness(id, target);
            }

            var reads = fixture.Snapshot();
            Refresh(flyout);
            CollectionAssert.AreEquivalent(ids, AssertFixtureInventory(flyout, 2), "Refresh changed stable monitor identities.");
            foreach (var id in ids)
            {
                var connector = VirtualMonitorFixture.ConnectorForId(id);
                Assert.AreEqual(connector == 0 ? 31d : 73d, ReadBrightness(flyout, FindMonitorBrightnessSlider(flyout, id)));
                Assert.IsTrue(fixture.Snapshot().Monitors[connector].Gets > reads.Monitors[connector].Gets, "Rediscovery did not read brightness through native DDC/CI.");
            }
        }
        finally
        {
            foreach (var id in ids)
            {
                var connector = VirtualMonitorFixture.ConnectorForId(id);
                var before = fixture.Snapshot();
                FindMonitorBrightnessSlider(flyout, id).SetValue(original.Monitors[connector].Brightness);
                AssertFixtureBrightness(fixture, connector, original.Monitors[connector].Brightness, before.Monitors[connector].Sets, requireWrite: false);
            }
        }

        SaveFixtureEvidence(fixture, "independent-brightness.json");
    }

    [TestMethod]
    [TestCategory("PowerDisplayVirtualMonitors")]
    public void VirtualMonitorHotPlugPreservesIdentityAndBrightness()
    {
        var fixture = RequireFixture();
        var flyout = OpenFlyout();
        var ids = AssertFixtureInventory(flyout, 2);
        var before = fixture.Snapshot();
        Step("Removing only virtual connector 1 through IddCxMonitorDeparture");
        try
        {
            fixture.SetConnected(1, false);
            AssertFixtureInventory(flyout, 1);
            Refresh(flyout);
            AssertFixtureInventory(flyout, 1);
            Assert.AreEqual(before.Monitors[0].Brightness, fixture.Snapshot().Monitors[0].Brightness);
            Assert.IsTrue(fixture.Snapshot().Monitors[1].Departures > before.Monitors[1].Departures, "The driver did not observe departure.");
            Step("Reconnecting the same virtual monitor");
            fixture.SetConnected(1, true);
            var returned = AssertFixtureInventory(flyout, 2);
            CollectionAssert.AreEquivalent(ids, returned, "Hot-plug changed monitor identity.");
            var id = returned.Single(value => VirtualMonitorFixture.ConnectorForId(value) == 1);
            Assert.AreEqual((double)before.Monitors[1].Brightness, ReadBrightness(flyout, FindMonitorBrightnessSlider(flyout, id)), "Arrival lost independent brightness state.");
            Assert.IsTrue(fixture.Snapshot().Monitors[1].Arrivals > before.Monitors[1].Arrivals, "The driver did not observe arrival.");
        }
        finally
        {
            fixture.SetConnected(1, true);
        }

        SaveFixtureEvidence(fixture, "hotplug.json");
    }

    [TestMethod]
    [TestCategory("PowerDisplayVirtualMonitors")]
    public void RemovingBothVirtualMonitorsShowsEmptyInventoryAndRecovers()
    {
        var fixture = RequireFixture();
        var flyout = OpenFlyout();
        var ids = AssertFixtureInventory(flyout, 2);
        try
        {
            Step("Removing both virtual monitors to exercise the no-supported-monitors state");
            fixture.SetConnected(0, false);
            fixture.SetConnected(1, false);
            AssertFixtureInventory(flyout, 0);
            Refresh(flyout);
            Assert.AreEqual(0, BrightnessSliders(flyout).Length, "The isolated guest should have no other DDC/CI-capable displays.");
            Assert.IsTrue(flyout.Has(By.AccessibilityId("NoMonitorsInfoBar")), "The empty inventory is not explained to the user.");
        }
        finally
        {
            fixture.Reset();
        }

        CollectionAssert.AreEquivalent(ids, AssertFixtureInventory(flyout, 2));
        SaveFixtureEvidence(fixture, "empty-and-recovered.json");
    }

    [TestMethod]
    [TestCategory("PowerDisplayVirtualMonitors")]
    public void UnsupportedVirtualBrightnessIsNotExposedAsAWorkingSlider()
    {
        var fixture = RequireFixture();
        var flyout = OpenFlyout();
        var ids = AssertFixtureInventory(flyout, 2);
        var unsupportedId = ids.Single(id => VirtualMonitorFixture.ConnectorForId(id) == 1);
        var before = fixture.Snapshot();
        try
        {
            Step("Advertising no brightness capability on virtual connector 1");
            fixture.SetMode(1, 1);
            Refresh(flyout);
            Assert.AreEqual(0, BrightnessSliders(flyout).Count(slider => string.Equals(slider.AutomationId, unsupportedId, StringComparison.OrdinalIgnoreCase)), "Unsupported brightness must not be presented as a controllable feature.");
            Assert.AreEqual(before.Monitors[1].Brightness, fixture.Snapshot().Monitors[1].Brightness, "Capability discovery changed brightness.");
            Assert.AreEqual(before.Monitors[1].Sets, fixture.Snapshot().Monitors[1].Sets, "Unsupported discovery wrote brightness.");
        }
        finally
        {
            fixture.SetMode(1, 0);
            Refresh(flyout);
        }

        AssertFixtureInventory(flyout, 2);
        SaveFixtureEvidence(fixture, "unsupported-brightness.json");
    }

    [TestMethod]
    [TestCategory("PowerDisplayVirtualMonitors")]
    public void FailedVirtualBrightnessWriteDoesNotPersistFalseSuccess()
    {
        var fixture = RequireFixture();
        var flyout = OpenFlyout();
        var ids = AssertFixtureInventory(flyout, 2);
        var id = ids.Single(value => VirtualMonitorFixture.ConnectorForId(value) == 0);
        var before = fixture.Snapshot();
        var original = before.Monitors[0].Brightness;
        var target = original < 80 ? original + 10 : original - 10;
        try
        {
            Step("Making virtual connector 0 reject real I2C traffic");
            fixture.SetMode(0, 2);
            FindMonitorBrightnessSlider(flyout, id).SetValue(target);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => fixture.Snapshot().Monitors[0].Failures,
                    failures => failures > before.Monitors[0].Failures,
                    15_000,
                    2).Succeeded,
                "The attempted UI write did not reach the failing I2C callback.");
            Assert.AreEqual(original, fixture.Snapshot().Monitors[0].Brightness, "A failed write changed the emulated monitor.");
            Assert.AreEqual(before.Monitors[0].Sets, fixture.Snapshot().Monitors[0].Sets, "The fixture falsely recorded a successful write.");
        }
        finally
        {
            fixture.SetMode(0, 0);
        }

        Refresh(flyout);
        Assert.AreEqual((double)before.Monitors[0].Brightness, ReadBrightness(flyout, FindMonitorBrightnessSlider(flyout, id)), "Refresh reported the failed write as hardware success.");
        Assert.IsTrue(ReadSavedBrightness(id) != target, "A rejected hardware write was persisted as a successful brightness change.");
        SaveFixtureEvidence(fixture, "communication-failure.json");
    }

    private static VirtualMonitorFixture RequireFixture()
    {
        Assert.IsNotNull(virtualMonitors, "BLOCKED: provision the isolated IDD and set POWERTOYS_POWERDISPLAY_IDD_PROBE. Simulation coverage must never silently skip.");
        return virtualMonitors;
    }

    private string[] AssertFixtureInventory(Session flyout, int expected)
    {
        var result = WaitHelper.WaitForStable(
            () =>
            {
                if (!FlyoutVisible())
                {
                    flyout = OpenFlyout();
                }

                return flyout.Has(By.AccessibilityId("ScanningMonitorsTextBlock"), 0)
                    ? null
                    : BrightnessSliders(flyout).Select(slider => slider.AutomationId)
                        .Where(VirtualMonitorFixture.IsFixtureId).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            },
            ids => ids is not null && ids.Length == expected,
            60_000,
            3,
            300,
            shouldRetryException: exception => exception is IOException or JsonException);
        Assert.IsTrue(result.Succeeded, $"Expected {expected} fixture monitors; observed {JsonSerializer.Serialize(result.LastObservation)}.");
        AssertStableInventory(flyout);
        return result.LastObservation!;
    }

    private static void AssertFixtureBrightness(VirtualMonitorFixture fixture, int connector, int expected, int previousWrites, bool requireWrite = true)
    {
        var result = WaitHelper.WaitForStable(
            () => fixture.Snapshot().Monitors[connector],
            monitor => monitor is not null && monitor.Brightness == expected && (!requireWrite || monitor.Sets > previousWrites),
            15_000,
            2,
            200);
        Assert.IsTrue(result.Succeeded, $"Fixture {connector} did not observe brightness {expected}: {JsonSerializer.Serialize(result.LastObservation)}.");
    }

    private void SaveFixtureEvidence(VirtualMonitorFixture fixture, string name)
    {
        var path = EvidencePath(name);
        File.WriteAllText(path, JsonSerializer.Serialize(fixture.Snapshot()));
        TestContext.AddResultFile(path);
    }
}
