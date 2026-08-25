// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class ContinuousVcpInitializerTests
{
    [TestMethod]
    public void Initialize_LiveInitialValueDoesNotReadAgain()
    {
        // The reader is primed with a failure it must never reach: if the initializer re-reads a
        // code the probe already answered, this test fails on the CallCount assertion rather than
        // on a fabricated value. The probed range is deliberately not 0-100, so both the raw
        // maximum and the percent scaling have to survive the seam for the assertions to hold.
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(1));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();
        var evidence = Evidence(new VcpInitialValue(
            new VcpFeatureValue(15, 0, 50),
            IsLive: true));

        var result = initializer.Initialize(monitor, evidence);

        Assert.IsTrue(result);
        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(30, monitor.CurrentBrightness);
        Assert.AreEqual(50, monitor.BrightnessVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_OmittedCodeCachedValueUsesFreshLiveValueAndPersists()
    {
        // The probe only runs when the capabilities string is unusable, so on the caps-parsed path
        // nothing has confirmed the cached value. It must be re-read before it is trusted,
        // otherwise the cache entry could never be refreshed.
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(60, 100));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();
        var evidence = CachedEvidence(parsedAdvertisesBrightness: false);

        initializer.Initialize(monitor, evidence);

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(60, monitor.CurrentBrightness);
        Assert.AreEqual(100, monitor.BrightnessVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.AreEqual(60, store.LastFeature!.Current);
        Assert.AreEqual(100, store.LastFeature.Maximum);
    }

    [TestMethod]
    public void Initialize_ProbeExhaustedCachedCodeDoesNotReadAgain()
    {
        // The probe already issued transactions for this code in this pass — it stopped on a
        // definitive DDCCI_VCP_NOT_SUPPORTED rather than exhausting the budget — so re-reading it
        // here would be pure I2C noise.
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(1));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();

        initializer.Initialize(monitor, ProbeExhaustedCachedEvidence());

        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(45, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_AdvertisedCachedCodeUsesCacheWhenLiveReadFails()
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
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
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(60, 0));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
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
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(55, 100));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();

        initializer.Initialize(
            monitor,
            new VcpDiscoveryEvidence(string.Empty, new VcpCapabilities(), new Dictionary<byte, VcpInitialValue>()));

        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(55, monitor.CurrentBrightness);
        Assert.AreEqual(55, store.LastFeature!.Current);
        Assert.AreEqual(100, store.LastFeature.Maximum);
    }

    [TestMethod]
    public void Initialize_InvalidRangeDoesNotApplyOrPersist()
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(55, 0));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessMonitor();

        var result = initializer.Initialize(
            monitor,
            new VcpDiscoveryEvidence(string.Empty, new VcpCapabilities(), new Dictionary<byte, VcpInitialValue>()));

        Assert.IsTrue(
            result,
            "An unusable range is the device's answer about one code, not about the handle, so the monitor must survive.");
        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(0, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.IsNull(store.LastFeature);
    }

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void Initialize_PhysicalMonitorUnavailableRejectsCacheAndStopsRemainingReads(int errorCode)
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(errorCode));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessAndContrastMonitor();
        var initialBrightness = monitor.CurrentBrightness;
        var initialContrast = monitor.CurrentContrast;

        var result = initializer.Initialize(
            monitor,
            CachedBrightnessAndContrastEvidence());

        Assert.IsFalse(result, "A handle-class read failure must tell the caller to drop the monitor.");
        Assert.AreEqual(1, reader.CallCount);
        CollectionAssert.AreEqual(new byte[] { 0x10 }, reader.Codes);
        Assert.AreEqual(initialBrightness, monitor.CurrentBrightness);
        Assert.AreEqual(initialContrast, monitor.CurrentContrast);
        Assert.AreEqual(MonitorReadFlags.None, monitor.ReadValues);
        Assert.IsNull(store.LastFeature);
    }

    [TestMethod]
    public void Initialize_VcpNotSupportedUsesCacheAndContinuesRemainingReads()
    {
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
            VcpReadAttempt.Success(60, 100));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = BrightnessAndContrastMonitor();

        var result = initializer.Initialize(
            monitor,
            CachedBrightnessAndContrastEvidence());

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x12 }, reader.Codes);
        Assert.AreEqual(45, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.AreEqual(60, monitor.CurrentContrast);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Contrast));
        Assert.AreEqual(0x12, store.LastFeature!.Code);
    }

    [TestMethod]
    public void Initialize_EveryContinuousCodeIsReadAndApplied()
    {
        // Pins the invariant ContinuousVcpInitializer's own remarks declare but nothing else
        // enforced: every entry in ContinuousVcpCodes needs an arm in both IsSupported and
        // ApplyValue. Without an IsSupported arm the code is never read, which the Codes assertion
        // catches; without an ApplyValue arm it is read and then discarded, which the per-feature
        // assertions catch. Ranges and percentages are all distinct so a cross-wired arm cannot
        // pass by coincidence.
        Assert.AreEqual(
            3,
            NativeConstants.ContinuousVcpCodes.Length,
            "ContinuousVcpCodes grew — give the new code an IsSupported and an ApplyValue arm, then extend this test.");

        var reader = new RecordingVcpReader(
            VcpReadAttempt.Success(15, 50),
            VcpReadAttempt.Success(20, 40),
            VcpReadAttempt.Success(7, 10));
        var initializer = new ContinuousVcpInitializer(reader, new RecordingKnownGoodStore());
        var monitor = AllContinuousMonitor();

        var result = initializer.Initialize(monitor, EmptyEvidence());

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(NativeConstants.ContinuousVcpCodes, reader.Codes);

        Assert.AreEqual(30, monitor.CurrentBrightness);
        Assert.AreEqual(50, monitor.BrightnessVcpMax);
        Assert.AreEqual(50, monitor.CurrentContrast);
        Assert.AreEqual(40, monitor.ContrastVcpMax);
        Assert.AreEqual(70, monitor.CurrentVolume);
        Assert.AreEqual(10, monitor.VolumeVcpMax);
        Assert.AreEqual(
            MonitorReadFlags.Brightness | MonitorReadFlags.Contrast | MonitorReadFlags.Volume,
            monitor.ReadValues);
    }

    [TestMethod]
    public void Initialize_ProbedVolumeIsAppliedWithoutReadingAgain()
    {
        // Volume is the one continuous code the probe path is not otherwise exercised against, and
        // it is the one whose ApplyValue arm has no neighbour to shadow a mistake.
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(1));
        var store = new RecordingKnownGoodStore();
        var initializer = new ContinuousVcpInitializer(reader, store);
        var monitor = VolumeMonitor();

        var result = initializer.Initialize(
            monitor,
            new VcpDiscoveryEvidence(
                string.Empty,
                new VcpCapabilities(),
                new Dictionary<byte, VcpInitialValue>
                {
                    [0x62] = new VcpInitialValue(new VcpFeatureValue(7, 0, 10), IsLive: true),
                }));

        Assert.IsTrue(result);
        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(70, monitor.CurrentVolume);
        Assert.AreEqual(10, monitor.VolumeVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Volume));
        Assert.IsNull(store.LastFeature);
    }

    private static Monitor BrightnessMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi | MonitorCapabilities.Brightness,
    };

    private static Monitor BrightnessAndContrastMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi |
            MonitorCapabilities.Brightness |
            MonitorCapabilities.Contrast,
    };

    private static Monitor VolumeMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi | MonitorCapabilities.Volume,
    };

    private static Monitor AllContinuousMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi |
            MonitorCapabilities.Brightness |
            MonitorCapabilities.Contrast |
            MonitorCapabilities.Volume,
    };

    /// <summary>
    /// Builds evidence that carries no value at all, so every supported code owes the hardware a
    /// read. <see cref="ContinuousVcpInitializer"/> gates on the fixture monitor's
    /// <see cref="MonitorCapabilities"/> flags rather than on the capabilities object here, which is
    /// why an empty one is enough.
    /// </summary>
    private static VcpDiscoveryEvidence EmptyEvidence() =>
        new(string.Empty, new VcpCapabilities(), new Dictionary<byte, VcpInitialValue>());

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
            cached: CachedBrightness());
    }

    private static VcpDiscoveryEvidence ProbeExhaustedCachedEvidence() =>
        VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: new Dictionary<byte, VcpProbeObservation>
            {
                [0x10] = VcpProbeObservation.Indeterminate(0x10, DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported, attempts: 3),
            },
            cached: CachedBrightness());

    private static Dictionary<byte, KnownGoodVcpFeature> CachedBrightness() => new()
    {
        [0x10] = Cached(0x10, current: 45, maximum: 100),
    };

    private static VcpDiscoveryEvidence CachedBrightnessAndContrastEvidence()
    {
        var capabilities = new VcpCapabilities();
        capabilities.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");
        capabilities.SupportedVcpCodes[0x12] = new VcpCodeInfo(0x12, "Contrast");

        return new VcpDiscoveryEvidence(
            string.Empty,
            capabilities,
            new Dictionary<byte, VcpInitialValue>
            {
                [0x10] = new VcpInitialValue(
                    new VcpFeatureValue(45, 0, 100),
                    IsLive: false,
                    PreferLiveRead: true),
            });
    }
}
