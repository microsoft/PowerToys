// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureRegistryTests
{
    [TestMethod]
    public void Brightness_HasThreeCandidatesInPriorityOrder()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x10, 0x13, 0x6B },
            new List<byte>(VcpFeatureRegistry.Candidates(VcpFeature.Brightness)));
    }

    [TestMethod]
    public void SingleCandidateFeatures_MatchTheirStandardCode()
    {
        Assert.AreEqual((byte)0x12, VcpFeatureRegistry.Primary(VcpFeature.Contrast));
        Assert.AreEqual((byte)0x62, VcpFeatureRegistry.Primary(VcpFeature.Volume));
        Assert.AreEqual((byte)0x14, VcpFeatureRegistry.Primary(VcpFeature.ColorTemperature));
        Assert.AreEqual((byte)0x60, VcpFeatureRegistry.Primary(VcpFeature.InputSource));
        Assert.AreEqual((byte)0xD6, VcpFeatureRegistry.Primary(VcpFeature.PowerState));
    }

    [TestMethod]
    public void Primary_IsFirstCandidate()
    {
        Assert.AreEqual((byte)0x10, VcpFeatureRegistry.Primary(VcpFeature.Brightness));
    }

    [TestMethod]
    public void AllFeatures_ContainsEverySixFeatures()
    {
        Assert.AreEqual(6, VcpFeatureRegistry.AllFeatures.Count);
    }

    [TestMethod]
    public void Key_RoundTripsThroughTryParseKey()
    {
        foreach (var feature in VcpFeatureRegistry.AllFeatures)
        {
            Assert.IsTrue(VcpFeatureRegistry.TryParseKey(VcpFeatureRegistry.Key(feature), out var parsed));
            Assert.AreEqual(feature, parsed);
        }
    }

    [TestMethod]
    public void TryParseKey_UnknownKey_ReturnsFalse()
    {
        Assert.IsFalse(VcpFeatureRegistry.TryParseKey("nonsense", out _));
    }
}
