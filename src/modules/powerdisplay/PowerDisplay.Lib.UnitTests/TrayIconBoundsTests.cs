// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayIconBoundsTests
{
    [TestMethod]
    public void Contains_UsesLeftTopInclusiveAndRightBottomExclusive()
    {
        var bounds = new TrayIconBounds(10, 20, 30, 40);

        Assert.IsTrue(bounds.Contains(10, 20));
        Assert.IsTrue(bounds.Contains(29, 39));
        Assert.IsFalse(bounds.Contains(30, 39));
        Assert.IsFalse(bounds.Contains(29, 40));
    }

    [TestMethod]
    public void IsValid_RejectsEmptyOrInvertedRectangles()
    {
        Assert.IsTrue(new TrayIconBounds(10, 20, 30, 40).IsValid);
        Assert.IsFalse(new TrayIconBounds(10, 20, 10, 40).IsValid);
        Assert.IsFalse(new TrayIconBounds(10, 20, 30, 20).IsValid);
        Assert.IsFalse(new TrayIconBounds(30, 40, 10, 20).IsValid);
    }
}
