// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureCodeMapTests
{
    [TestMethod]
    public void SetCode_MarksSupported_AndReturnsCode()
    {
        var map = new VcpFeatureCodeMap();
        map.SetCode(VcpFeature.Brightness, 0x6B);

        Assert.IsTrue(map.IsResolved(VcpFeature.Brightness));
        Assert.IsTrue(map.IsSupported(VcpFeature.Brightness));
        Assert.AreEqual((byte)0x6B, map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void SetNotSupported_IsResolvedButNotSupported()
    {
        var map = new VcpFeatureCodeMap();
        map.SetNotSupported(VcpFeature.Volume);

        Assert.IsTrue(map.IsResolved(VcpFeature.Volume));
        Assert.IsFalse(map.IsSupported(VcpFeature.Volume));
    }

    [TestMethod]
    public void GetCode_WhenUnresolved_FallsBackToRegistryPrimary()
    {
        var map = new VcpFeatureCodeMap();
        Assert.AreEqual(VcpFeatureRegistry.Primary(VcpFeature.Brightness), map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void GetCode_WhenNotSupported_FallsBackToRegistryPrimary()
    {
        var map = new VcpFeatureCodeMap();
        map.SetNotSupported(VcpFeature.Brightness);
        Assert.AreEqual(VcpFeatureRegistry.Primary(VcpFeature.Brightness), map.GetCode(VcpFeature.Brightness));
    }

    [TestMethod]
    public void ToPersisted_UsesStringKeysAndSentinel()
    {
        var map = new VcpFeatureCodeMap();
        map.SetCode(VcpFeature.Brightness, 0x6B);
        map.SetNotSupported(VcpFeature.Volume);

        var persisted = map.ToPersisted();

        Assert.AreEqual(0x6B, persisted["brightness"]);
        Assert.AreEqual(-1, persisted["volume"]);
    }

    [TestMethod]
    public void FromPersisted_RoundTripsValuesAndSentinel()
    {
        var source = new Dictionary<string, int> { ["brightness"] = 0x13, ["contrast"] = -1 };
        var map = VcpFeatureCodeMap.FromPersisted(source);

        Assert.IsTrue(map.IsSupported(VcpFeature.Brightness));
        Assert.AreEqual((byte)0x13, map.GetCode(VcpFeature.Brightness));
        Assert.IsTrue(map.IsResolved(VcpFeature.Contrast));
        Assert.IsFalse(map.IsSupported(VcpFeature.Contrast));
    }

    [TestMethod]
    public void FromPersisted_Null_ReturnsEmptyMap()
    {
        var map = VcpFeatureCodeMap.FromPersisted(null);
        Assert.IsFalse(map.IsResolved(VcpFeature.Brightness));
    }

    [TestMethod]
    public void FromPersisted_IgnoresUnknownKeys()
    {
        var map = VcpFeatureCodeMap.FromPersisted(new Dictionary<string, int> { ["future"] = 5 });
        Assert.IsFalse(map.IsResolved(VcpFeature.Brightness));
    }

    [TestMethod]
    public void FromPersisted_OutOfRangeValues_AreIgnored()
    {
        var map = VcpFeatureCodeMap.FromPersisted(new Dictionary<string, int>
        {
            ["brightness"] = 300,   // > 255
            ["contrast"] = -2,      // negative, not the sentinel
            ["volume"] = -1,        // valid sentinel
            ["colorTemperature"] = 0x14, // valid code
        });

        Assert.IsFalse(map.IsResolved(VcpFeature.Brightness));
        Assert.IsFalse(map.IsResolved(VcpFeature.Contrast));
        Assert.IsTrue(map.IsResolved(VcpFeature.Volume));
        Assert.IsFalse(map.IsSupported(VcpFeature.Volume));
        Assert.AreEqual((byte)0x14, map.GetCode(VcpFeature.ColorTemperature));
    }
}
