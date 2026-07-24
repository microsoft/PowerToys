// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

[TestClass]
public class SdrContentBrightnessLevelTests
{
    [DataTestMethod]
    [DataRow(0, 1000u)]
    [DataRow(1, 1050u)]
    [DataRow(50, 3500u)]
    [DataRow(100, 6000u)]
    public void ToRaw_MapsSystemSliderRange(int percentage, uint expected)
    {
        Assert.AreEqual(expected, SdrContentBrightnessLevel.ToRaw(percentage));
    }

    [DataTestMethod]
    [DataRow(1000u, 0)]
    [DataRow(1050u, 1)]
    [DataRow(3500u, 50)]
    [DataRow(6000u, 100)]
    public void FromRaw_MapsSystemSliderRange(uint rawValue, int expected)
    {
        Assert.AreEqual(expected, SdrContentBrightnessLevel.FromRaw(rawValue));
    }

    [TestMethod]
    public void Conversion_ClampsOutOfRangeValues()
    {
        Assert.AreEqual(1000u, SdrContentBrightnessLevel.ToRaw(-10));
        Assert.AreEqual(6000u, SdrContentBrightnessLevel.ToRaw(110));
        Assert.AreEqual(0, SdrContentBrightnessLevel.FromRaw(0));
        Assert.AreEqual(100, SdrContentBrightnessLevel.FromRaw(7000));
    }
}
