// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class WindowControlTests
{
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
