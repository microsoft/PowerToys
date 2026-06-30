// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureResolverTests
{
    private static VcpCapabilities CapsWith(params byte[] codes)
    {
        var caps = new VcpCapabilities();
        foreach (var code in codes)
        {
            caps.SupportedVcpCodes[code] = new VcpCodeInfo(code, "test");
        }

        return caps;
    }

    private static Func<byte, bool> NeverProbe(List<byte> log) => code =>
    {
        log.Add(code);
        return false;
    };

    [TestMethod]
    public void Phase1_PicksFirstCandidatePresentInCaps()
    {
        var caps = CapsWith(0x10, 0x12, 0x62);
        var probeLog = new List<byte>();

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: NeverProbe(probeLog));

        Assert.AreEqual((byte)0x10, map.GetCode(VcpFeature.Brightness));
        Assert.AreEqual(0, probeLog.Count, "Normal mode must never probe.");
    }

    [TestMethod]
    public void Phase1_FallsToLowerPriorityCandidate()
    {
        // 0x10 absent, 0x6B present -> brightness resolves to 0x6B.
        var caps = CapsWith(0x6B);

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: _ => false);

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        Assert.IsTrue(map.IsSupported(VcpFeature.Brightness));
    }

    [TestMethod]
    public void Phase1_HonorsPriorityWhenMultiplePresent()
    {
        // VcpFeatureRegistry.Candidates(VcpFeature.Brightness) == [0x10, 0x13, 0x6B] (priority order).
        var caps = CapsWith(0x13, 0x6B); // both alternates present, no 0x10
        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: _ => false);
        Assert.AreEqual((byte)0x13, map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void NormalMode_NoCandidate_ResolvesNotSupported_WithoutProbing()
    {
        var caps = CapsWith(0x12); // contrast only; brightness candidates absent
        var probeLog = new List<byte>();

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: false, persisted: null, probe: NeverProbe(probeLog));

        Assert.IsFalse(map.IsSupported(VcpFeature.Brightness));
        Assert.IsTrue(map.IsResolved(VcpFeature.Brightness));
        Assert.AreEqual(0, probeLog.Count);
    }

    [TestMethod]
    public void MaxCompat_ProbesInPriorityOrder_AndStopsAtFirstSuccess()
    {
        // VcpFeatureRegistry.Candidates(VcpFeature.Brightness) == [0x10, 0x13, 0x6B] (priority order).
        var caps = CapsWith(); // empty: no candidate for any feature
        var probeLog = new List<byte>();
        Func<byte, bool> probe = code =>
        {
            probeLog.Add(code);
            return code == 0x6B; // only the third brightness candidate responds
        };

        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: true, persisted: null, probe: probe);

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));

        // Assert the first 3 brightness probes happened in order 0x10, 0x13, 0x6B (stops at 0x6B for brightness).
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x13, 0x6B }, probeLog.GetRange(0, 3));
    }

    [TestMethod]
    public void MaxCompat_NoCandidateResponds_ResolvesNotSupported()
    {
        var caps = CapsWith();
        var map = VcpFeatureResolver.Resolve(caps, maxCompatibilityMode: true, persisted: null, probe: _ => false);
        Assert.IsFalse(map.IsSupported(VcpFeature.Brightness));
        Assert.IsTrue(map.IsResolved(VcpFeature.Brightness));
    }

    [TestMethod]
    public void Persisted_IsReusedVerbatim_WithoutCapsOrProbe()
    {
        var persisted = new VcpFeatureCodeMap();
        persisted.SetCode(VcpFeature.Brightness, 0x6B);
        persisted.SetNotSupported(VcpFeature.Volume);

        var probeLog = new List<byte>();

        // caps says brightness is on 0x10, but persisted (0x6B) must win.
        var map = VcpFeatureResolver.Resolve(CapsWith(0x10, 0x62), maxCompatibilityMode: true, persisted: persisted, probe: NeverProbe(probeLog));

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        Assert.IsFalse(map.IsSupported(VcpFeature.Volume));
        Assert.IsTrue(map.IsResolved(VcpFeature.Volume));
        Assert.AreEqual(0, probeLog.Count, "Reused features must not be probed.");
    }

    [TestMethod]
    public void Persisted_PartialMap_ResolvesOnlyMissingFeatures()
    {
        var persisted = new VcpFeatureCodeMap();
        persisted.SetCode(VcpFeature.Brightness, 0x6B); // only brightness persisted

        // contrast resolved fresh from caps; brightness reused.
        var map = VcpFeatureResolver.Resolve(CapsWith(0x12), maxCompatibilityMode: false, persisted: persisted, probe: _ => false);

        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
        Assert.IsTrue(map.IsSupported(VcpFeature.Contrast));
        Assert.AreEqual((byte)0x12, map.GetCode(VcpFeature.Contrast));
    }
}
