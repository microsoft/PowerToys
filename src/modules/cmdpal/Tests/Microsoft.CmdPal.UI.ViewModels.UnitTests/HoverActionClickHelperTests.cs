// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class HoverActionClickHelperTests
{
    [TestMethod]
    public void GetActionIndexFromX_MapsSlots()
    {
        Assert.AreEqual(0, HoverActionClickHelper.GetActionIndexFromX(0));
        Assert.AreEqual(0, HoverActionClickHelper.GetActionIndexFromX(29));
        Assert.AreEqual(1, HoverActionClickHelper.GetActionIndexFromX(30));
        Assert.AreEqual(2, HoverActionClickHelper.GetActionIndexFromX(75));
    }

    [TestMethod]
    public void TryGetActionAtIndex_ReturnsExpectedItem()
    {
        var actions = new[] { "info", "edit", "pin" };

        Assert.AreEqual("info", HoverActionClickHelper.TryGetActionAtIndex(actions, 5));
        Assert.AreEqual("edit", HoverActionClickHelper.TryGetActionAtIndex(actions, 35));
        Assert.AreEqual("pin", HoverActionClickHelper.TryGetActionAtIndex(actions, 65));
        Assert.IsNull(HoverActionClickHelper.TryGetActionAtIndex(actions, 95));
        Assert.IsNull(HoverActionClickHelper.TryGetActionAtIndex(actions, -1));
    }

    [TestMethod]
    public void IsPointInsideHoverList_RespectsBounds()
    {
        Assert.IsTrue(HoverActionClickHelper.IsPointInsideHoverList(0, 0, 90, 28));
        Assert.IsTrue(HoverActionClickHelper.IsPointInsideHoverList(90, 28, 90, 28));
        Assert.IsFalse(HoverActionClickHelper.IsPointInsideHoverList(91, 14, 90, 28));
        Assert.IsFalse(HoverActionClickHelper.IsPointInsideHoverList(10, -1, 90, 28));
    }
}
