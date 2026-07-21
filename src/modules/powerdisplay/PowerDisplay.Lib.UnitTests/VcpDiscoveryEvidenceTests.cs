// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class VcpDiscoveryEvidenceTests
{
    private static readonly DateTime CachedUtc = new(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);

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
            cached: cache,
            includeCache: true);

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(40, result.InitialValues[0x10].Value.Current);
        Assert.IsTrue(result.InitialValues[0x10].IsLive);
    }

    [TestMethod]
    public void Reconcile_IndeterminateLiveUsesCachedPositiveEvidence()
    {
        var live = new Dictionary<byte, VcpProbeObservation>
        {
            [0x10] = VcpProbeObservation.Indeterminate(0x10, unchecked((int)0xC0262589)),
        };
        var cache = new Dictionary<byte, KnownGoodVcpFeature>
        {
            [0x10] = Cached(0x10, current: 25),
        };

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: live,
            cached: cache,
            includeCache: true);

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(25, result.InitialValues[0x10].Value.Current);
        Assert.IsFalse(result.InitialValues[0x10].IsLive);
    }

    [TestMethod]
    public void Reconcile_NormalModeIgnoresCache()
    {
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x10] = Cached(0x10, 25) },
            includeCache: false);

        Assert.IsNull(result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
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
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x10] = Cached(0x10, 25) },
            includeCache: true);

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
        Assert.AreEqual(1, result.InitialValues.Count);
        Assert.AreEqual(25, result.InitialValues[0x10].Value.Current);
        Assert.IsFalse(result.InitialValues[0x10].IsLive);
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
            cached: new Dictionary<byte, KnownGoodVcpFeature> { [0x10] = cached },
            includeCache: true);

        Assert.IsNull(result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
    }

    [TestMethod]
    public void Reconcile_NoLiveOrCacheLeavesFeatureUnavailable()
    {
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: new Dictionary<byte, VcpProbeObservation>(),
            cached: new Dictionary<byte, KnownGoodVcpFeature>(),
            includeCache: true);

        Assert.IsNull(result.Capabilities);
    }

    private static KnownGoodVcpFeature Cached(byte code, int current) => new()
    {
        Code = code,
        Current = current,
        Maximum = 100,
        Source = VcpObservationSource.MaximumCompatibilityProbe,
        LastSuccessfulUtc = CachedUtc,
    };
}
