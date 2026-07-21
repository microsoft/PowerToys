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
    public void Initialize_OmittedCodeCachedValueDoesNotReadOrClaimFreshRead()
    {
        var reader = new RecordingReader(VcpReadAttempt.Failure(1));
        var store = new RecordingStore();
        var initializer = new ContinuousVcpInitializer(reader, store, new FixedClock());
        var monitor = BrightnessMonitor();
        var evidence = CachedEvidence(parsedAdvertisesBrightness: false);

        initializer.Initialize(monitor, new IntPtr(1), evidence);

        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(45, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_AdvertisedCachedCodeUsesFreshLiveValueAndPersists()
    {
        var reader = new RecordingReader(VcpReadAttempt.Success(60, 100));
        var store = new RecordingStore();
        var clock = new FixedClock();
        var initializer = new ContinuousVcpInitializer(reader, store, clock);
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
            new IntPtr(1),
            CachedEvidence(parsedAdvertisesBrightness: true));

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(60, monitor.CurrentBrightness);
        Assert.AreEqual(100, monitor.BrightnessVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.AreEqual(60, store.LastFeature!.Current);
        Assert.AreEqual(100, store.LastFeature.Maximum);
        Assert.AreEqual(VcpObservationSource.CapabilitiesInitialization, store.LastFeature.Source);
        Assert.AreEqual(clock.UtcNow, store.LastFeature.LastSuccessfulUtc);
    }

    [TestMethod]
    public void Initialize_AdvertisedCachedCodeUsesCacheWhenLiveReadFails()
    {
        var reader = new RecordingReader(VcpReadAttempt.Failure(unchecked((int)0xC0262589)));
        var store = new RecordingStore();
        var initializer = new ContinuousVcpInitializer(reader, store, new FixedClock());
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
            new IntPtr(1),
            CachedEvidence(parsedAdvertisesBrightness: true));

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(45, monitor.CurrentBrightness);
        Assert.AreEqual(100, monitor.BrightnessVcpMax);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_AdvertisedCachedCodeUsesCacheWhenLiveRangeIsInvalid()
    {
        var reader = new RecordingReader(VcpReadAttempt.Success(60, 0));
        var store = new RecordingStore();
        var initializer = new ContinuousVcpInitializer(reader, store, new FixedClock());
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
            new IntPtr(1),
            CachedEvidence(parsedAdvertisesBrightness: true));

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(45, monitor.CurrentBrightness);
        Assert.AreEqual(100, monitor.BrightnessVcpMax);
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

    private static VcpDiscoveryEvidence CachedEvidence(bool parsedAdvertisesBrightness)
    {
        var parsedCapabilities = new VcpCapabilities();
        var parsedCode = parsedAdvertisesBrightness ? (byte)0x10 : (byte)0x12;
        parsedCapabilities.SupportedVcpCodes[parsedCode] =
            new VcpCodeInfo(parsedCode, parsedAdvertisesBrightness ? "Brightness" : "Contrast");

        return VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: parsedCapabilities,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature>
            {
                [0x10] = new KnownGoodVcpFeature
                {
                    Code = 0x10,
                    Current = 45,
                    Maximum = 100,
                    Source = VcpObservationSource.MaximumCompatibilityProbe,
                    LastSuccessfulUtc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc),
                },
            },
            includeCache: true);
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
