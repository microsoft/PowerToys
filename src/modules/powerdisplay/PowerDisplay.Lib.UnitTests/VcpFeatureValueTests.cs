// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpFeatureValueTests
{
    [TestMethod]
    public void ToPercentage_StandardRange_MapsCurrentToPercent()
    {
        var value = new VcpFeatureValue(current: 75, minimum: 0, maximum: 100);
        Assert.AreEqual(75, value.ToPercentage());
    }

    [TestMethod]
    public void ToPercentage_SamsungHalfRange_MapsCurrentToPercent()
    {
        // Samsung S27DG602SN style: native max=50.
        var value = new VcpFeatureValue(current: 25, minimum: 0, maximum: 50);
        Assert.AreEqual(50, value.ToPercentage());
    }

    [TestMethod]
    public void ToPercentage_DegenerateRange_ReturnsZero()
    {
        var value = new VcpFeatureValue(current: 10, minimum: 5, maximum: 5);
        Assert.AreEqual(0, value.ToPercentage());
    }

    [TestMethod]
    public void ToPercentage_Invalid_ReturnsZero()
    {
        Assert.AreEqual(0, VcpFeatureValue.Invalid.ToPercentage());
    }

    [TestMethod]
    public void FromPercentage_StandardRange_IsIdentity()
    {
        Assert.AreEqual(0, VcpFeatureValue.FromPercentage(0, maximum: 100));
        Assert.AreEqual(50, VcpFeatureValue.FromPercentage(50, maximum: 100));
        Assert.AreEqual(100, VcpFeatureValue.FromPercentage(100, maximum: 100));
    }

    [TestMethod]
    public void FromPercentage_SamsungHalfRange_ScalesToHalf()
    {
        // The bug: with native max=50, slider 80% must produce raw 40, not 80.
        Assert.AreEqual(0, VcpFeatureValue.FromPercentage(0, maximum: 50));
        Assert.AreEqual(25, VcpFeatureValue.FromPercentage(50, maximum: 50));
        Assert.AreEqual(40, VcpFeatureValue.FromPercentage(80, maximum: 50));
        Assert.AreEqual(50, VcpFeatureValue.FromPercentage(100, maximum: 50));
    }

    [TestMethod]
    public void FromPercentage_OversizedRange_ScalesProportionally()
    {
        // Some high-end displays advertise max=255.
        Assert.AreEqual(0, VcpFeatureValue.FromPercentage(0, maximum: 255));
        Assert.AreEqual(128, VcpFeatureValue.FromPercentage(50, maximum: 255));
        Assert.AreEqual(255, VcpFeatureValue.FromPercentage(100, maximum: 255));
    }

    [TestMethod]
    public void FromPercentage_NonZeroMinimum_OffsetsResult()
    {
        Assert.AreEqual(10, VcpFeatureValue.FromPercentage(0, maximum: 60, minimum: 10));
        Assert.AreEqual(35, VcpFeatureValue.FromPercentage(50, maximum: 60, minimum: 10));
        Assert.AreEqual(60, VcpFeatureValue.FromPercentage(100, maximum: 60, minimum: 10));
    }

    [TestMethod]
    public void FromPercentage_OutOfRangePercent_IsClamped()
    {
        Assert.AreEqual(0, VcpFeatureValue.FromPercentage(-50, maximum: 100));
        Assert.AreEqual(100, VcpFeatureValue.FromPercentage(250, maximum: 100));
    }

    [TestMethod]
    public void FromPercentage_DegenerateRange_ReturnsMinimum()
    {
        Assert.AreEqual(0, VcpFeatureValue.FromPercentage(75, maximum: 0));
        Assert.AreEqual(7, VcpFeatureValue.FromPercentage(75, maximum: 7, minimum: 7));
    }

    [TestMethod]
    public void RoundTrip_IsApproximatelyStable()
    {
        // ToPercentage and FromPercentage are inverses up to integer rounding error.
        const int max = 50;
        for (int raw = 0; raw <= max; raw++)
        {
            var pct = new VcpFeatureValue(raw, 0, max).ToPercentage();
            var roundTripped = VcpFeatureValue.FromPercentage(pct, max);
            Assert.IsTrue(
                System.Math.Abs(roundTripped - raw) <= 1,
                $"raw={raw} → pct={pct} → roundTripped={roundTripped} (drift > 1)");
        }
    }
}
