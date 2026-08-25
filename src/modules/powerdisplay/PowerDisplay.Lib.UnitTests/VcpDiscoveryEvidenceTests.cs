// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class VcpDiscoveryEvidenceTests
{
    private static readonly int[] VolumePresets = { 0x00, 0x10, 0x20, 0x30 };

    [TestMethod]
    public void Reconcile_LiveObservationOverridesCache()
    {
        var live = new Dictionary<byte, VcpProbeObservation>
        {
            [0x10] = VcpProbeObservation.Success(0x10, new VcpFeatureValue(40, 0, 100)),
        };
        var cache = new Dictionary<byte, KnownGoodVcpFeature>
        {
            [0x10] = Cached(0x10, current: 20),
        };

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: live,
            cached: cache);

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(40, result.InitialValues[0x10].Value.Current);
        Assert.IsTrue(result.InitialValues[0x10].IsLive);
    }

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void Reconcile_PhysicalMonitorUnavailableRejectsAllCapabilitiesAndCache(int errorCode)
    {
        var parsedCapabilities = new VcpCapabilities();
        parsedCapabilities.SupportedVcpCodes[0x12] = new VcpCodeInfo(0x12, "Contrast");
        var live = new Dictionary<byte, VcpProbeObservation>
        {
            [0x10] = VcpProbeObservation.Success(0x10, new VcpFeatureValue(40, 0, 100)),
            [0x12] = VcpProbeObservation.Indeterminate(0x12, errorCode),
        };
        var cache = new Dictionary<byte, KnownGoodVcpFeature>
        {
            [0x62] = Cached(0x62, current: 25),
        };

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: "(vcp(12))",
            parsedCapabilities: parsedCapabilities,
            live: live,
            cached: cache);

        Assert.IsTrue(result.IsPhysicalMonitorUnavailable);
        Assert.IsNull(result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
    }

    [TestMethod]
    public void Reconcile_VcpNotSupportedStillUsesCachedPositiveEvidence()
    {
        var live = new Dictionary<byte, VcpProbeObservation>
        {
            [0x10] = VcpProbeObservation.Indeterminate(0x10, DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
        };

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: live,
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x10] = Cached(0x10, 25) });

        Assert.IsFalse(result.IsPhysicalMonitorUnavailable);
        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(25, result.InitialValues[0x10].Value.Current);
        Assert.IsFalse(result.InitialValues[0x10].IsLive);

        // The probe spent its full retry budget on 0x10 this cycle; re-reading it is pointless.
        Assert.IsFalse(result.InitialValues[0x10].PreferLiveRead);
    }

    [TestMethod]
    public void Reconcile_MaximumCompatibilityUnionsParsedCapabilitiesWithExactIdCache()
    {
        var parsedCapabilities = new VcpCapabilities();
        parsedCapabilities.SupportedVcpCodes[0x12] = new VcpCodeInfo(0x12, "Contrast");

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: parsedCapabilities,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x10] = Cached(0x10, 25) });

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
        Assert.AreEqual(1, result.InitialValues.Count);
        Assert.AreEqual(25, result.InitialValues[0x10].Value.Current);
        Assert.IsFalse(result.InitialValues[0x10].IsLive);

        // No probe ran for 0x10 in this cycle (the caps string parsed), so the cached value still
        // owes the hardware one read before it is trusted and re-persisted.
        Assert.IsTrue(result.InitialValues[0x10].PreferLiveRead);
    }

    [TestMethod]
    public void Reconcile_MaximumCompatibilityKeepsParsedDiscreteValuesWhenCacheSupplementsCode()
    {
        // 0x62 is advertised with a discrete value list. Cache evidence may add support for a code
        // but must never downgrade metadata the capabilities string already parsed.
        var parsedCapabilities = new VcpCapabilities();
        parsedCapabilities.SupportedVcpCodes[0x62] =
            new VcpCodeInfo(0x62, "Audio: Speaker Volume", VolumePresets);

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: "(vcp(62(00 10 20 30)))",
            parsedCapabilities: parsedCapabilities,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x62] = Cached(0x62, 25) });

        var codeInfo = result.Capabilities!.GetVcpCodeInfo(0x62)!.Value;
        Assert.AreEqual("Audio: Speaker Volume", codeInfo.Name);
        Assert.IsTrue(codeInfo.HasDiscreteValues);
        CollectionAssert.AreEqual(VolumePresets, codeInfo.SupportedValues.ToArray());
    }

    [TestMethod]
    public void Reconcile_RepliedProbeWithUnusableRangeStillAdvertisesSupport()
    {
        // The device answered 0x10 — unimplemented codes fail with DDCCI_VCP_NOT_SUPPORTED instead
        // — but reported a range that cannot scale a percentage. Support is proven even though the
        // value is not, so the feature must stay reachable.
        var live = new Dictionary<byte, VcpProbeObservation>
        {
            [0x10] = VcpProbeObservation.Indeterminate(0x10, lastError: null, attempts: 3, replied: true),
        };

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: live,
            cached: new Dictionary<byte, KnownGoodVcpFeature>());

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(0, result.InitialValues.Count);
    }

    [TestMethod]
    public void Reconcile_UnansweredProbeDoesNotAdvertiseSupport()
    {
        var live = new Dictionary<byte, VcpProbeObservation>
        {
            [0x10] = VcpProbeObservation.Indeterminate(0x10, DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
        };

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: live,
            cached: new Dictionary<byte, KnownGoodVcpFeature>());

        Assert.IsNull(result.Capabilities);
    }

    [TestMethod]
    public void Reconcile_ProbedCodeOutsideTheDefaultSweepIsStillHonoured()
    {
        // Reconcile is driven by what the probe reported, not by NativeConstants.ContinuousVcpCodes,
        // so widening VcpFeatureProbeService's constructor-injected sweep list does not silently drop
        // a code that answered. Honouring it here is necessary but not sufficient: the carried value
        // is only consumed for codes ContinuousVcpInitializer walks, so a widened sweep still needs a
        // matching edit there.
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: Observations((0x60, VcpProbeObservation.Success(0x60, new VcpFeatureValue(0x11, 0, 0x12)))),
            cached: new Dictionary<byte, KnownGoodVcpFeature>());

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x60));
        Assert.AreEqual(0x11, result.InitialValues[0x60].Value.Current);
    }

    [TestMethod]
    public void Reconcile_ParsedCapabilitiesSurviveWhenNoProbeRan()
    {
        // The probe only runs when the caps string is unusable, so the parsed path must be a
        // pass-through once the cache is empty too: no codes added, no values invented.
        var parsed = new VcpCapabilities();
        parsed.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: "caps",
            parsedCapabilities: parsed,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature>());

        Assert.AreSame(parsed, result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
        Assert.AreEqual("caps", result.CapabilitiesRaw);
    }

    [TestMethod]
    public void Reconcile_InvalidCachedRangeIsIgnored()
    {
        var cached = Cached(0x10, current: 25);
        cached.Maximum = 0;

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x10] = cached });

        Assert.IsNull(result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
    }

    [TestMethod]
    public void Reconcile_ReportsOnlyTheCodesTheCacheAloneProved()
    {
        // The caps string advertises 0x10 and the probe replied for 0x12, so only 0x62 rests on
        // persisted evidence — that is the one discovery has to name in the log, because it is the
        // only control a user could see without the hardware ever having claimed to support it.
        var parsedCapabilities = new VcpCapabilities();
        parsedCapabilities.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: parsedCapabilities,
            live: new Dictionary<byte, VcpProbeObservation>
            {
                [0x12] = VcpProbeObservation.Indeterminate(0x12, lastError: null, attempts: 3, replied: true),
            },
            cached: new Dictionary<byte, KnownGoodVcpFeature>
            {
                [0x10] = Cached(0x10, 25),
                [0x12] = Cached(0x12, 35),
                [0x62] = Cached(0x62, 45),
            });

        CollectionAssert.AreEqual(new byte[] { 0x62 }, result.CacheSupplementedCodes.ToArray());
        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x62));

        // 0x10 is advertised by the caps string and cached, and no probe touched it, so it is not
        // cache-supplemented but still owes the hardware one read before the cached value is trusted.
        Assert.IsTrue(result.InitialValues[0x10].PreferLiveRead);
    }

    private static Dictionary<byte, VcpProbeObservation> Observations(
        params (byte Code, VcpProbeObservation Observation)[] entries)
    {
        var observations = new Dictionary<byte, VcpProbeObservation>();
        foreach (var (code, observation) in entries)
        {
            observations[code] = observation;
        }

        return observations;
    }

    private static KnownGoodVcpFeature Cached(byte code, int current) => new()
    {
        Code = code,
        Current = current,
        Maximum = 100,
    };
}
