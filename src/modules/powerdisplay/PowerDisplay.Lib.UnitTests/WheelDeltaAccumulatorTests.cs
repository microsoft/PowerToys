// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class WheelDeltaAccumulatorTests
{
    [TestMethod]
    public void Add_FullPositiveNotch_ReturnsOne()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(1, accumulator.Add(120));
    }

    [TestMethod]
    public void Add_FullNegativeNotch_ReturnsMinusOne()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(-1, accumulator.Add(-120));
    }

    [TestMethod]
    public void Add_MultipleNotchesInOnePacket_ReturnsAllNotches()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(3, accumulator.Add(360));
    }

    [TestMethod]
    public void Add_PartialPackets_EmitsOnlyAfterCompleteNotch()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(0, accumulator.Add(30));
        Assert.AreEqual(0, accumulator.Add(30));
        Assert.AreEqual(0, accumulator.Add(30));
        Assert.AreEqual(1, accumulator.Add(30));
    }

    [TestMethod]
    public void Add_DirectionReversal_CancelsPartialRemainder()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(0, accumulator.Add(80));
        Assert.AreEqual(0, accumulator.Add(-40));
        Assert.AreEqual(0, accumulator.Add(-40));
    }

    [TestMethod]
    public void Reset_DropsPartialRemainder()
    {
        var accumulator = new WheelDeltaAccumulator();
        Assert.AreEqual(0, accumulator.Add(60));

        accumulator.Reset();

        Assert.AreEqual(0, accumulator.Add(60));
    }
}
