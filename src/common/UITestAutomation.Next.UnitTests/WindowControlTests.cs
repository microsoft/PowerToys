// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class WindowControlTests
{
    private static readonly string[] ExpectedCaptionClickActions = ["check", "move", "check", "click"];

    [TestMethod]
    public void CaptionHitTestUsesTheOwnedChildRatherThanTheClientRoot()
    {
        var root = new IntPtr(1);
        var child = new IntPtr(2);
        Assert.IsTrue(WindowControl.IsExposedCaption(
            root,
            child,
            _ => root,
            target => target == child ? 2 : 1));
    }

    [TestMethod]
    public void CaptionHitTestRejectsAnotherWindowsCaption()
    {
        Assert.IsFalse(WindowControl.IsExposedCaption(
            new IntPtr(1),
            new IntPtr(2),
            _ => new IntPtr(3),
            _ => throw new AssertFailedException("A foreign window must not be hit-tested or clicked.")));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(20)]
    public void CaptionHitTestRejectsClientContentAndCaptionButtons(int hit)
    {
        var window = new IntPtr(1);
        Assert.IsFalse(WindowControl.IsExposedCaption(window, window, _ => window, _ => hit));
    }

    [TestMethod]
    public void CaptionRecoveryDoesNotClickCoveredOrNonCaptionPoints()
    {
        Assert.IsFalse(WindowControl.TryClickExposedCaption(
            (0, 0, 800, 600),
            (_, _) => false,
            (_, _) => throw new AssertFailedException("No safe caption was found; the pointer must not move."),
            () => Assert.Fail("No safe caption was found; no click is allowed.")));
    }

    [TestMethod]
    public void CaptionRecoveryRechecksOwnershipAfterMovingBeforeOneClick()
    {
        var actions = new List<string>();
        Assert.IsTrue(WindowControl.TryClickExposedCaption(
            (0, 0, 800, 600),
            (_, _) =>
            {
                actions.Add("check");
                return true;
            },
            (_, _) =>
            {
                actions.Add("move");
                return true;
            },
            () => actions.Add("click")));
        CollectionAssert.AreEqual(ExpectedCaptionClickActions, actions);
    }

    [TestMethod]
    public void CaptionRecoveryDoesNotClickAfterOwnershipChanges()
    {
        var checks = 0;
        Assert.IsFalse(WindowControl.TryClickExposedCaption(
            (0, 0, 800, 600),
            (_, _) => ++checks == 1,
            (_, _) => true,
            () => Assert.Fail("Caption ownership changed after pointer movement.")));
        Assert.AreEqual(2, checks);
    }

    [TestMethod]
    public void CaptionRecoveryDoesNotClickWhenPointerDoesNotSettle()
    {
        Assert.IsFalse(WindowControl.TryClickExposedCaption(
            (0, 0, 800, 600),
            (_, _) => true,
            (_, _) => false,
            () => Assert.Fail("The pointer did not reach the verified caption point.")));
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, false)]
    [DataRow(5, false)]
    [DataRow(6, false)]
    public void KeyboardFocusRequiresTheExpectedClassWithinTheExpectedWindow(int focusedWindow, bool expected)
    {
        var windows = new Dictionary<IntPtr, (IntPtr Parent, string ClassName)>
        {
            [new(2)] = (new(1), "SHELLDLL_DefView"),
            [new(3)] = (new(2), "DirectUIHWND"),
            [new(4)] = (new(1), "NamespaceTreeControl"),
            [new(5)] = (new(6), "SHELLDLL_DefView"),
            [new(6)] = (IntPtr.Zero, "CabinetWClass"),
        };

        var actual = WindowControl.IsKeyboardFocusWithinClass(
            new IntPtr(1),
            "shelldll_defview",
            new IntPtr(focusedWindow),
            child => windows[child].Parent,
            child => windows[child].ClassName);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void KeyboardFocusRejectsAnUnspecifiedWindow()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WindowControl.IsKeyboardFocusWithinClass(IntPtr.Zero, "SHELLDLL_DefView"));
    }

    [TestMethod]
    public void KeyboardFocusRejectsAnUnspecifiedClass()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WindowControl.IsKeyboardFocusWithinClass(new IntPtr(1), string.Empty));
    }
}
