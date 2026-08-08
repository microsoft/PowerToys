// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class HoverActionTabNavigationTests
{
    [TestMethod]
    public void TryHandleForward_FirstTabHighlightsRowOnly()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = false;

        var handled = HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.IsTrue(rowTabFocused);
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TryHandleForward_SecondTabSelectsFirstIcon()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = true;

        var handled = HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.IsFalse(rowTabFocused);
        Assert.AreEqual(0, index);
    }

    [TestMethod]
    public void TryHandleForward_FromRowThroughStripAndExit()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = false;

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.IsTrue(rowTabFocused);

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.AreEqual(0, index);

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.AreEqual(1, index);

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.AreEqual(2, index);

        Assert.IsFalse(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TryHandleForward_AfterForwardExit_CanReEnterRowStep()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = false;

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));
        Assert.IsFalse(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true));

        var handled = HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.IsTrue(rowTabFocused);
    }

    [TestMethod]
    public void TryHandleBackward_FromFirstIconReturnsToRowStep()
    {
        var index = 0;
        var rowTabFocused = false;

        var handled = HoverActionTabNavigation.TryHandleBackward(ref index, ref rowTabFocused, 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.IsTrue(rowTabFocused);
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TryHandleBackward_FromRowStepLeavesStrip()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = true;

        var handled = HoverActionTabNavigation.TryHandleBackward(ref index, ref rowTabFocused, 3, stripVisible: true);

        Assert.IsFalse(handled);
        Assert.IsFalse(rowTabFocused);
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TryHandleBackward_FromNoSelectionSelectsLastIcon()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = false;

        var handled = HoverActionTabNavigation.TryHandleBackward(ref index, ref rowTabFocused, 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.IsFalse(rowTabFocused);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void TryHandleForward_RowAndIconNeverBothFocused()
    {
        var index = HoverActionSelection.NoSelection;
        var rowTabFocused = false;

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 2, stripVisible: true));
        Assert.IsTrue(rowTabFocused);
        Assert.AreEqual(HoverActionSelection.NoSelection, index);

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 2, stripVisible: true));
        Assert.IsFalse(rowTabFocused);
        Assert.AreEqual(0, index);

        Assert.IsTrue(HoverActionTabNavigation.TryHandleForward(ref index, ref rowTabFocused, 2, stripVisible: true));
        Assert.IsFalse(rowTabFocused);
        Assert.AreEqual(1, index);
    }
}
