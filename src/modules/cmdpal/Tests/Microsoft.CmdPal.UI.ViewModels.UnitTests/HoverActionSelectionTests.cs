// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class HoverActionSelectionTests
{
    [TestMethod]
    public void TrySelectNext_FromNoSelection_SelectsFirst()
    {
        var index = HoverActionSelection.NoSelection;

        var handled = HoverActionSelection.TrySelectNext(ref index, actionCount: 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.AreEqual(0, index);
    }

    [TestMethod]
    public void TrySelectNext_AdvancesThroughStrip()
    {
        var index = 0;

        Assert.IsTrue(HoverActionSelection.TrySelectNext(ref index, 3, stripVisible: true));
        Assert.AreEqual(1, index);

        Assert.IsTrue(HoverActionSelection.TrySelectNext(ref index, 3, stripVisible: true));
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void TrySelectNext_AtLastIcon_ClearsAndReturnsFalse()
    {
        var index = 2;

        var handled = HoverActionSelection.TrySelectNext(ref index, 3, stripVisible: true);

        Assert.IsFalse(handled);
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TrySelectNext_HiddenStrip_ReturnsFalse()
    {
        var index = HoverActionSelection.NoSelection;

        Assert.IsFalse(HoverActionSelection.TrySelectNext(ref index, 3, stripVisible: false));
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TrySelectPrev_FromMiddle_SelectsPrevious()
    {
        var index = 2;

        Assert.IsTrue(HoverActionSelection.TrySelectPrev(ref index, 3, stripVisible: true));
        Assert.AreEqual(1, index);
    }

    [TestMethod]
    public void TrySelectPrev_FromFirst_ClearsAndReturnsFalse()
    {
        var index = 0;

        var handled = HoverActionSelection.TrySelectPrev(ref index, 3, stripVisible: true);

        Assert.IsFalse(handled);
        Assert.AreEqual(HoverActionSelection.NoSelection, index);
    }

    [TestMethod]
    public void TrySelectPrev_FromNoSelection_ReturnsFalse()
    {
        var index = HoverActionSelection.NoSelection;

        Assert.IsFalse(HoverActionSelection.TrySelectPrev(ref index, 3, stripVisible: true));
    }

    [TestMethod]
    public void TrySelectLastOnBackwardEntry_SelectsLastIcon()
    {
        var index = HoverActionSelection.NoSelection;

        var handled = HoverActionSelection.TrySelectLastOnBackwardEntry(ref index, 3, stripVisible: true);

        Assert.IsTrue(handled);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void TrySelectLastOnBackwardEntry_WhenAlreadySelected_ReturnsFalse()
    {
        var index = 1;

        Assert.IsFalse(HoverActionSelection.TrySelectLastOnBackwardEntry(ref index, 3, stripVisible: true));
        Assert.AreEqual(1, index);
    }
}
