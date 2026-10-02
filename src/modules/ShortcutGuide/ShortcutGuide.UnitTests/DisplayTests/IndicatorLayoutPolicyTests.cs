// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;

namespace ShortcutGuide.UnitTests.DisplayTests;

[TestClass]
public sealed class IndicatorLayoutPolicyTests
{
    [TestMethod]
    [DataRow(48.0, 1.0f, 40.0)]
    [DataRow(60.0, 1.5f, 36.0)]
    [DataRow(40.0, 1.0f, 36.0)]
    [DataRow(24.0, 1.0f, 28.0)]
    public void GetBodySizeDip_ConvertsSlotAndClampsToReadableRange(
        double smallestSlotPhysical,
        float dpiScale,
        double expectedBodyDip)
    {
        double actual = IndicatorLayoutPolicy.GetBodySizeDip(smallestSlotPhysical, dpiScale);

        Assert.AreEqual(expectedBodyDip, actual);
    }
}
