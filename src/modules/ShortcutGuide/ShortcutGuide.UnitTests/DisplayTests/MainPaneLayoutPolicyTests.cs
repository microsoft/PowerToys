// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;

namespace ShortcutGuide.UnitTests.DisplayTests;

[TestClass]
public sealed class MainPaneLayoutPolicyTests
{
    [TestMethod]
    [DataRow(0, false, false, 70.0, 16.0, 16.0, 16.0)]
    [DataRow(0, true, false, 16.0, 16.0, 16.0, 16.0)]
    [DataRow(1, false, false, 16.0, 62.0, 16.0, 16.0)]
    [DataRow(1, true, false, 16.0, 62.0, 16.0, 16.0)]
    [DataRow(2, false, false, 16.0, 16.0, 16.0, 16.0)]
    [DataRow(2, true, false, 16.0, 16.0, 70.0, 16.0)]
    [DataRow(3, false, false, 16.0, 16.0, 16.0, 16.0)]
    [DataRow(3, true, false, 16.0, 16.0, 16.0, 16.0)]
    [DataRow(3, false, true, 16.0, 16.0, 16.0, 62.0)]
    [DataRow(3, true, true, 16.0, 16.0, 16.0, 62.0)]
    public void GetMargins_ReservesIndicatorSpaceForTaskbarEdge(
        int edgeValue,
        bool isRightAligned,
        bool reserveBottomForIndicators,
        double expectedLeft,
        double expectedTop,
        double expectedRight,
        double expectedBottom)
    {
        MainPaneMargins margins = MainPaneLayoutPolicy.GetMargins(
            (TaskbarEdge)edgeValue, isRightAligned, reserveBottomForIndicators);

        Assert.AreEqual(expectedLeft, margins.Left);
        Assert.AreEqual(expectedTop, margins.Top);
        Assert.AreEqual(expectedRight, margins.Right);
        Assert.AreEqual(expectedBottom, margins.Bottom);
    }
}
