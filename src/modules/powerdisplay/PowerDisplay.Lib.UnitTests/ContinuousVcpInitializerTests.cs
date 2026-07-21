// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class ContinuousVcpInitializerTests
{
    [TestMethod]
    public void Initialize_LiveInitialValueDoesNotReadAgain()
    {
        var reader = new RecordingReader(VcpReadAttempt.Failure(1));
        var store = new RecordingStore();
        var initializer = new ContinuousVcpInitializer(reader, store, new FixedClock());
        var monitor = BrightnessMonitor();
        var evidence = Evidence(new VcpInitialValue(
            new VcpFeatureValue(30, 0, 100),
            VcpObservationSource.MaximumCompatibilityProbe,
            IsLive: true));

        initializer.Initialize(monitor, new IntPtr(1), evidence);

        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(30, monitor.CurrentBrightness);
        Assert.AreEqual(100, monitor.BrightnessVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_CachedInitialValueDoesNotReadOrClaimFreshRead()
    {
        var reader = new RecordingReader(VcpReadAttempt.Failure(1));
        var store = new RecordingStore();
        var initializer = new ContinuousVcpInitializer(reader, store, new FixedClock());
        var monitor = BrightnessMonitor();
        var evidence = Evidence(new VcpInitialValue(
            new VcpFeatureValue(45, 0, 100),
            VcpObservationSource.MaximumCompatibilityProbe,
            IsLive: false));

        initializer.Initialize(monitor, new IntPtr(1), evidence);

        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(45, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_NoInitialValueReadsOnceAndPersistsSuccess()
    {
        var reader = new RecordingReader(VcpReadAttempt.Success(55, 100));
        var store = new RecordingStore();
        var clock = new FixedClock();
        var initializer = new ContinuousVcpInitializer(reader, store, clock);
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
            new IntPtr(1),
            new VcpDiscoveryEvidence(string.Empty, new VcpCapabilities(), new Dictionary<byte, VcpInitialValue>()));

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(55, monitor.CurrentBrightness);
        Assert.AreEqual(VcpObservationSource.CapabilitiesInitialization, store.LastFeature!.Source);
        Assert.AreEqual(clock.UtcNow, store.LastFeature.LastSuccessfulUtc);
    }

    [TestMethod]
    public void Initialize_InvalidRangeDoesNotApplyOrPersist()
    {
        var reader = new RecordingReader(VcpReadAttempt.Success(55, 0));
        var store = new RecordingStore();
        var initializer = new ContinuousVcpInitializer(reader, store, new FixedClock());
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
            new IntPtr(1),
            new VcpDiscoveryEvidence(string.Empty, new VcpCapabilities(), new Dictionary<byte, VcpInitialValue>()));

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(0, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    private static Monitor BrightnessMonitor() => new()
    {
        Id = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1",
        Capabilities = MonitorCapabilities.DdcCi | MonitorCapabilities.Brightness,
    };

    private static VcpDiscoveryEvidence Evidence(VcpInitialValue value)
    {
        var capabilities = new VcpCapabilities();
        capabilities.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");
        return new VcpDiscoveryEvidence(
            string.Empty,
            capabilities,
            new Dictionary<byte, VcpInitialValue> { [0x10] = value });
    }

    private sealed class RecordingReader(VcpReadAttempt result) : IVcpFeatureReader
    {
        public int CallCount { get; private set; }

        public VcpReadAttempt Read(IntPtr handle, byte code)
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingStore : IKnownGoodVcpStore
    {
        public KnownGoodVcpFeature? LastFeature { get; private set; }

        public IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId) =>
            new Dictionary<byte, KnownGoodVcpFeature>();

        public void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature) =>
            LastFeature = feature.Clone();
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTime UtcNow { get; } = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);
    }
}
