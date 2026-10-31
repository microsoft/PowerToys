// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class ScrollDirectionHelperTests
{
    [TestMethod]
    public void ApplyReverse_WhenDisabled_ReturnsDeltaUnchanged()
    {
        Assert.AreEqual(120, ScrollDirectionHelper.ApplyReverse(120, false, WM.WM_MOUSEWHEEL));
        Assert.AreEqual(-120, ScrollDirectionHelper.ApplyReverse(-120, false, WM.WM_MOUSEWHEEL));
    }

    [TestMethod]
    public void ApplyReverse_WhenEnabledAndVerticalWheel_NegatesDelta()
    {
        Assert.AreEqual(-120, ScrollDirectionHelper.ApplyReverse(120, true, WM.WM_MOUSEWHEEL));
        Assert.AreEqual(120, ScrollDirectionHelper.ApplyReverse(-120, true, WM.WM_MOUSEWHEEL));
        Assert.AreEqual(0, ScrollDirectionHelper.ApplyReverse(0, true, WM.WM_MOUSEWHEEL));
    }

    [TestMethod]
    public void ApplyReverse_WhenEnabledButNotVerticalWheel_ReturnsDeltaUnchanged()
    {
        // The WheelDelta field is reused for XBUTTON identity and horizontal-wheel
        // magnitude, so these events MUST pass through unchanged even when reversing.
        Assert.AreEqual(120, ScrollDirectionHelper.ApplyReverse(120, true, WM.WM_MOUSEHWHEEL));
        Assert.AreEqual(1, ScrollDirectionHelper.ApplyReverse(1, true, WM.WM_XBUTTONDOWN));
        Assert.AreEqual(2, ScrollDirectionHelper.ApplyReverse(2, true, WM.WM_XBUTTONUP));
        Assert.AreEqual(120, ScrollDirectionHelper.ApplyReverse(120, true, WM.WM_MOUSEMOVE));
    }
}
