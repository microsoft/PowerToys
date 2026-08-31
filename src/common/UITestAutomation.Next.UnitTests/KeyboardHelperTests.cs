// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public class KeyboardHelperTests
{
    [TestMethod]
    public void CreateChordPlanKeepsLeftControlSideSpecific()
    {
        var (chord, heldKeys) = KeyboardHelper.CreateChordPlan(Key.LCtrl, Key.A);

        Assert.AreEqual("a", chord);
        CollectionAssert.AreEqual(new[] { Key.LCtrl }, heldKeys.ToArray());
    }

    [TestMethod]
    public void CreateChordPlanKeepsRightControlSideSpecific()
    {
        var (chord, heldKeys) = KeyboardHelper.CreateChordPlan(Key.RCtrl, Key.A);

        Assert.AreEqual("a", chord);
        CollectionAssert.AreEqual(new[] { Key.RCtrl }, heldKeys.ToArray());
    }

    [TestMethod]
    public void SendKeysRejectsLayoutDependentOemKeys()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => KeyboardHelper.SendKeys(Key.OemPeriod));
    }
}
