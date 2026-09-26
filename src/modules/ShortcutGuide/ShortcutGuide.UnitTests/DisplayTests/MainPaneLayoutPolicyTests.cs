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
    [DataRow(0, false, 70.0, 16.0, 16.0)]
    [DataRow(0, true, 16.0, 16.0, 16.0)]
    [DataRow(1, false, 16.0, 70.0, 16.0)]
    [DataRow(1, true, 16.0, 70.0, 16.0)]
    [DataRow(2, false, 16.0, 16.0, 16.0)]
    [DataRow(2, true, 16.0, 16.0, 70.0)]
    [DataRow(3, false, 16.0, 16.0, 16.0)]
    [DataRow(3, true, 16.0, 16.0, 16.0)]
    public void GetMargins_ReservesIndicatorSpaceForTaskbarEdge(
        int edgeValue,
        bool isRightAligned,
        double expectedLeft,
        double expectedTop,
        double expectedRight)
    {
        MainPaneMargins margins = MainPaneLayoutPolicy.GetMargins((TaskbarEdge)edgeValue, isRightAligned);

        Assert.AreEqual(expectedLeft, margins.Left);
        Assert.AreEqual(expectedTop, margins.Top);
        Assert.AreEqual(expectedRight, margins.Right);
        Assert.AreEqual(16.0, margins.Bottom);
    }
}
