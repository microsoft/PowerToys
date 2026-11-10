// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>
/// Which screen the question and the choice window open on: the caret's, when the window being typed into reaches onto
/// another screen; otherwise that window's own; and the pointer's once the window has gone. Fakes stand in for Windows.
/// </summary>
[TestClass]
public sealed class PromptScreenTests
{
    private static readonly IntPtr Target = new(0x1234);
    private static readonly Native.RECT LeftScreen = Rect(0, 0, 1920, 1080);
    private static readonly Native.POINT Pointer = new() { X = 2500, Y = 400 };

    [TestMethod]
    public void AWindowOnOneScreenOpensItThereWithoutLookingForTheCaret()
    {
        var asked = new List<string>();
        PromptScreen.Choice choice = PromptScreen.Choose(Target, Fakes(asked, frame: Rect(100, 100, 1500, 900), caret: Rect(300, 200, 301, 218)));

        Assert.AreEqual(PromptScreen.Basis.Window, choice.Basis);
        CollectionAssert.DoesNotContain(asked, "caret", "Finding the caret can take a moment, and it's on the window's screen anyway.");
    }

    [TestMethod]
    public void AMaximizedWindowIsOnOneScreen()
    {
        // Its visible frame fills the work area; its invisible resize border, which reaches past the screen, isn't counted.
        var asked = new List<string>();
        Assert.AreEqual(PromptScreen.Basis.Window, PromptScreen.Choose(Target, Fakes(asked, frame: Rect(0, 0, 1920, 1032), caret: Rect(300, 200, 301, 218))).Basis);
        CollectionAssert.DoesNotContain(asked, "caret");
    }

    [TestMethod]
    public void AWindowAcrossTwoScreensOpensItOnTheCaretsScreen()
    {
        // Most of the window is on the left screen, and the caret is in its part on the right one (F6).
        PromptScreen.Choice choice = PromptScreen.Choose(Target, Fakes([], frame: Rect(400, 200, 2300, 900), caret: Rect(2100, 500, 2101, 518)));

        Assert.AreEqual(PromptScreen.Basis.Caret, choice.Basis);
        Assert.AreEqual(2100, choice.Point.X);
        Assert.AreEqual(509, choice.Point.Y, "The middle of the caret's height.");
    }

    [TestMethod]
    public void AWindowAcrossTwoScreensWithoutACaretOpensItOnTheWindowsScreen()
    {
        var asked = new List<string>();
        PromptScreen.Choice choice = PromptScreen.Choose(Target, Fakes(asked, frame: Rect(400, 200, 2300, 900), caret: null));

        Assert.AreEqual(PromptScreen.Basis.Window, choice.Basis, "The screen most of the window is on.");
        CollectionAssert.Contains(asked, "caret");
    }

    [TestMethod]
    public void AWindowThatHasGoneOpensItOnThePointersScreen()
    {
        var asked = new List<string>();
        PromptScreen.Choice choice = PromptScreen.Choose(Target, Fakes(asked, frame: null, caret: Rect(300, 200, 301, 218), exists: false));

        Assert.AreEqual(PromptScreen.Basis.Pointer, choice.Basis);
        Assert.AreEqual(Pointer, choice.Point);
        CollectionAssert.DoesNotContain(asked, "caret");
        Assert.AreEqual(PromptScreen.Basis.Pointer, PromptScreen.Choose(IntPtr.Zero, Fakes([], frame: null, caret: null)).Basis, "Nor without a window at all.");
    }

    [TestMethod]
    public void AWindowReachesOntoAnotherScreenOnlyPastAnEdge()
    {
        Assert.IsFalse(PromptScreen.Crosses(Rect(0, 0, 1920, 1080), LeftScreen), "Edge to edge is on the screen.");
        Assert.IsTrue(PromptScreen.Crosses(Rect(0, 0, 1921, 1080), LeftScreen));
        Assert.IsTrue(PromptScreen.Crosses(Rect(-1, 0, 1000, 1080), LeftScreen));
        Assert.IsTrue(PromptScreen.Crosses(Rect(10, -5, 1000, 600), LeftScreen));
        Assert.IsTrue(PromptScreen.Crosses(Rect(10, 500, 1000, 1081), LeftScreen));
    }

    private static PromptScreen.Sources Fakes(List<string> asked, Native.RECT? frame, Native.RECT? caret, bool exists = true)
        => new(
            _ => exists,
            _ => frame,
            _ => LeftScreen,
            _ =>
            {
                asked.Add("caret");
                return caret;
            },
            () => Pointer);

    private static Native.RECT Rect(int left, int top, int right, int bottom) => new() { Left = left, Top = top, Right = right, Bottom = bottom };
}
