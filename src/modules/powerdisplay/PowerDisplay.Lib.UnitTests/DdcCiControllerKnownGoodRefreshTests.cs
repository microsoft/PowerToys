// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers the known-good cache refresh a successful DDC write performs. The cache is otherwise
/// only written by a successful read, so without this a slider move leaves it holding the
/// pre-write value and maximum compatibility mode republishes that stale value on a later
/// discovery whose probe touched the code but could not read it.
/// </summary>
[TestClass]
public sealed class DdcCiControllerKnownGoodRefreshTests
{
    [TestMethod]
    public void RefreshKnownGoodAfterWrite_RefreshesCurrent()
    {
        var store = new RecordingKnownGoodStore(Cached(0x10, current: 45, maximum: 50));
        using var controller = new DdcCiController(store, new UnusedReader());

        // The device range is 0-50, so the write path scaled the slider against 50 and handed
        // SetVCPFeature a raw 20 — the same maximum the cache entry was established with.
        controller.RefreshKnownGoodAfterWrite(ScaledMonitor(brightnessVcpMax: 50), 0x10, 20);

        var refreshed = store.GetKnownGoodFeatures(MonitorId)[0x10];
        Assert.AreEqual(20, refreshed.Current);
        Assert.AreEqual(50, refreshed.Maximum);
    }

    [TestMethod]
    public void RefreshKnownGoodAfterWrite_PlaceholderMaximumIsRejected()
    {
        // A monitor whose discovery read failed still carries the placeholder BrightnessVcpMax of
        // 100 while the cache holds the read-proven device range of 50. Accepting the write here
        // would overwrite that range with the placeholder and mis-scale every later write.
        var store = new RecordingKnownGoodStore(Cached(0x10, current: 45, maximum: 50));
        using var controller = new DdcCiController(store, new UnusedReader());

        controller.RefreshKnownGoodAfterWrite(ScaledMonitor(brightnessVcpMax: 100), 0x10, 20);

        var untouched = store.GetKnownGoodFeatures(MonitorId)[0x10];
        Assert.AreEqual(45, untouched.Current);
        Assert.AreEqual(50, untouched.Maximum);
        Assert.AreEqual(0, store.UpsertCount);
    }

    [TestMethod]
    public void RefreshKnownGoodAfterWrite_WithoutCachedEntryCreatesNothing()
    {
        // A successful SetVCPFeature is not evidence that the device implements the code, so a
        // write must never establish a cache entry a read has not already proven.
        var store = new RecordingKnownGoodStore();
        using var controller = new DdcCiController(store, new UnusedReader());

        controller.RefreshKnownGoodAfterWrite(ScaledMonitor(brightnessVcpMax: 50), 0x10, 20);

        Assert.AreEqual(0, store.GetKnownGoodFeatures(MonitorId).Count);
        Assert.AreEqual(0, store.UpsertCount);
    }

    [TestMethod]
    public void RefreshKnownGoodAfterWrite_OutOfRangeValueIsRejected()
    {
        var store = new RecordingKnownGoodStore(Cached(0x10, current: 45, maximum: 50));
        using var controller = new DdcCiController(store, new UnusedReader());

        controller.RefreshKnownGoodAfterWrite(ScaledMonitor(brightnessVcpMax: 50), 0x10, 51);

        Assert.AreEqual(45, store.GetKnownGoodFeatures(MonitorId)[0x10].Current);
        Assert.AreEqual(0, store.UpsertCount);
    }

    [TestMethod]
    public void RefreshKnownGoodAfterWrite_DiscreteCodeIsRejected()
    {
        // Only the continuous codes carry a percent-scaled maximum; 0x14 is a discrete preset and
        // has no cache entry to keep aligned.
        var store = new RecordingKnownGoodStore(Cached(0x14, current: 5, maximum: 11));
        using var controller = new DdcCiController(store, new UnusedReader());

        controller.RefreshKnownGoodAfterWrite(ScaledMonitor(brightnessVcpMax: 50), 0x14, 6);

        Assert.AreEqual(5, store.GetKnownGoodFeatures(MonitorId)[0x14].Current);
        Assert.AreEqual(0, store.UpsertCount);
    }

    [TestMethod]
    public void RefreshKnownGoodAfterWrite_EmptyMonitorIdIsIgnored()
    {
        // The store is seeded and answers any Id, so the only thing that can keep the refresh from
        // happening is the production guard itself: drop `string.IsNullOrEmpty(monitor.Id)` and both
        // assertions flip.
        var store = new RecordingKnownGoodStore(Cached(0x10, current: 45, maximum: 50));
        using var controller = new DdcCiController(store, new UnusedReader());

        controller.RefreshKnownGoodAfterWrite(new Monitor { BrightnessVcpMax = 50 }, 0x10, 20);

        Assert.AreEqual(0, store.GetCallCount, "An empty Id must short-circuit before the store is queried.");
        Assert.AreEqual(0, store.UpsertCount);
    }

    private static Monitor ScaledMonitor(int brightnessVcpMax) => new()
    {
        Id = MonitorId,
        Capabilities = MonitorCapabilities.DdcCi | MonitorCapabilities.Brightness,
        BrightnessVcpMax = brightnessVcpMax,
    };

    /// <summary>
    /// The refresh path performs no VCP reads; a call here means the production code regressed.
    /// </summary>
    private sealed class UnusedReader : IVcpFeatureReader
    {
        public VcpReadAttempt Read(IntPtr handle, byte code) =>
            throw new InvalidOperationException("The known-good refresh path must not read VCP features.");
    }
}
