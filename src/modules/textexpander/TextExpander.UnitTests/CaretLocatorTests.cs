// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>
/// Where the notice finds the text cursor, in order: Windows' caret, UI Automation's caret range or selection, the
/// focused element's bottom-left, the window's bottom middle, then the pointer. Fakes stand in for Windows and the app.
/// </summary>
[TestClass]
public sealed class CaretLocatorTests
{
    private static readonly IntPtr Target = new(0x1234);
    private static readonly Native.RECT Window = Rect(100, 100, 900, 700);
    private static readonly Native.RECT SystemCaret = Rect(300, 200, 302, 218);
    private static readonly Native.RECT CaretRange = Rect(400, 300, 401, 318);
    private static readonly Native.RECT Selection = Rect(500, 400, 501, 418);
    private static readonly Native.RECT Element = Rect(200, 150, 600, 450);
    private static readonly string[] UpToAutomation = ["window", "system", "in front", "automation", "in front"];
    private static readonly string[] RangeOnly = ["range"];
    private static readonly string[] RangeThenNext = ["range", "next"];
    private static readonly string[] RangeNextPrevious = ["range", "next", "previous"];

    [TestMethod]
    public void UsesTheCaretWindowsKeepsFirst()
    {
        var asked = new List<string>();
        Native.RECT caret = CaretLocator.Locate(Target, Fakes(asked, SystemCaret, new(CaretRange, Selection, Element), Window, new() { X = 5, Y = 5 }));

        Assert.AreEqual(SystemCaret, caret);
        CollectionAssert.DoesNotContain(asked, "automation", "UI Automation, which asks the app, isn't needed.");
    }

    [TestMethod]
    public void AsksUiAutomationForAppsThatDrawTheirOwnCaret()
    {
        var asked = new List<string>();
        Native.RECT caret = CaretLocator.Locate(Target, Fakes(asked, null, new(Rect(400, 300, 400, 318), Selection, Element), Window));

        Assert.AreEqual(Rect(400, 300, 401, 318), caret, "A caret drawn as a line is made 1 pixel wide, never empty.");
        CollectionAssert.AreEqual(UpToAutomation, asked, "Nothing further is asked.");
    }

    [TestMethod]
    public void UsesTheSelectionWhenThereIsNoCaretRange()
        => Assert.AreEqual(Selection, CaretLocator.Locate(Target, Fakes([], null, new(null, Selection, Element), Window)));

    [TestMethod]
    public void FallsBackToTheFocusedElementsBottomLeft()
        => Assert.AreEqual(Rect(200, 449, 201, 450), CaretLocator.Locate(Target, Fakes([], null, new(null, null, Element), Window)));

    [TestMethod]
    public void KeepsTheFocusedElementsCornerInsideTheWindow()
    {
        // A scrolled document reaches far past its window, so its bottom-left put the notice far from the text.
        Native.RECT document = Rect(100, -500, 900, 3500);
        Assert.AreEqual(Rect(100, 699, 101, 700), CaretLocator.Locate(Target, Fakes([], null, new(null, null, document), Window)));
    }

    [TestMethod]
    public void UsesTheCaretOfAWindowLaidOutRightToLeft()
    {
        // Mapped to the screen in a mirrored window, the caret's left edge came out right of its right edge, so it was
        // taken for no caret, and the notice went to UI Automation's answer or the window's bottom.
        var asked = new List<string>();
        Native.RECT caret = CaretLocator.Locate(Target, Fakes(asked, Rect(302, 200, 300, 218), new(CaretRange, Selection, Element), Window));

        Assert.AreEqual(SystemCaret, caret);
        CollectionAssert.DoesNotContain(asked, "automation");
    }

    [TestMethod]
    public void FallsBackToTheTargetWindowsBottomMiddle()
    {
        Assert.AreEqual(Rect(500, 699, 501, 700), CaretLocator.Locate(Target, Fakes([], null, null, Window)), "UI Automation had no answer in time.");
        Assert.AreEqual(Rect(500, 699, 501, 700), CaretLocator.Locate(Target, Fakes([], null, new(null, null, null), Window)), "It answered, with nothing.");
    }

    [TestMethod]
    public void FallsBackToThePointerWhenTheWindowHasGone()
        => Assert.AreEqual(Rect(640, 359, 641, 360), CaretLocator.Locate(Target, Fakes([], null, null, null, new() { X = 640, Y = 360 })));

    [TestMethod]
    public void NeverReturnsAnEmptyRectangleAtTheOrigin()
    {
        Native.RECT last = CaretLocator.Locate(IntPtr.Zero, Fakes([], null, null, null, null));
        Assert.AreEqual(Rect(960, 519, 961, 520), last, "The middle of the primary work area.");

        Native.RECT origin = CaretLocator.Locate(IntPtr.Zero, Fakes([], null, null, null, new() { X = 0, Y = 0 }));
        Assert.IsTrue(origin.Right > origin.Left && origin.Bottom > origin.Top, "Even a pointer at 0,0 gives a rectangle with an area.");
    }

    [TestMethod]
    public void SkipsACaretThatCantBeRight()
    {
        // A caret with no height, as some apps leave when they hide it.
        Assert.AreEqual(CaretRange, CaretLocator.Locate(Target, Fakes([], Rect(300, 200, 301, 200), new(CaretRange, null, null), Window)));

        // A UI Automation caret outside the window means the focus is elsewhere now: its element and window are next.
        Native.RECT elsewhere = Rect(1500, 900, 1501, 918);
        Assert.AreEqual(Rect(200, 449, 201, 450), CaretLocator.Locate(Target, Fakes([], null, new(elsewhere, elsewhere, Element), Window)));
        Assert.AreEqual(Rect(500, 699, 501, 700), CaretLocator.Locate(Target, Fakes([], null, new(elsewhere, null, Rect(1200, 800, 1600, 1000)), Window)));

        // The bounds of a whole document aren't a caret.
        Assert.AreEqual(Rect(500, 699, 501, 700), CaretLocator.Locate(Target, Fakes([], null, new(Rect(120, 110, 121, 2500), null, null), Window)));

        // A caret just past the window's right edge, as in the window snapped beside it, isn't in it.
        Assert.AreEqual(Rect(200, 449, 201, 450), CaretLocator.Locate(Target, Fakes([], null, new(Rect(900, 300, 900, 318), null, Element), Window)));
        Assert.AreEqual(Rect(899, 300, 900, 318), CaretLocator.Locate(Target, Fakes([], null, new(Rect(899, 300, 899, 318), null, Element), Window)), "A pixel to the left, it is.");
    }

    [TestMethod]
    public void TrustsUiAutomationOnlyWhileTheTargetIsInFront()
    {
        // UI Automation reports the focus of whichever window is in front: with another one there, it's that window's.
        var asked = new List<string>();
        Native.RECT caret = CaretLocator.Locate(Target, Fakes(asked, null, new(CaretRange, Selection, Element), Window, inFront: () => false));

        Assert.AreEqual(Rect(500, 699, 501, 700), caret, "The target window's bottom middle.");
        CollectionAssert.DoesNotContain(asked, "automation");

        // The query can take a moment, and what it found is another window's if that came in front meanwhile.
        var answers = new Queue<bool>();
        answers.Enqueue(true);
        answers.Enqueue(false);
        Assert.AreEqual(Rect(500, 699, 501, 700), CaretLocator.Locate(Target, Fakes([], null, new(CaretRange, Selection, Element), Window, inFront: answers.Dequeue)));
    }

    [TestMethod]
    public void FindsTheCaretAtTheEdgeOfAUiAutomationRange()
    {
        // UI Automation gives rectangles as left, top, width and height.
        Assert.AreEqual(Rect(10, 20, 11, 36), CaretLocator.Edge([10.2, 20.5, 0, 15.5], atEnd: false));
        Assert.AreEqual(Rect(55, 40, 56, 55), CaretLocator.Edge([10, 20, 30, 15, 5, 40, 50, 15], atEnd: true), "The end of a selection over two lines.");
        Assert.IsNull(CaretLocator.Edge([], atEnd: false), "Many apps give an empty range no rectangles.");
        Assert.IsNull(CaretLocator.Edge(null, atEnd: false));
        Assert.IsNull(CaretLocator.Edge([10, 20, 0, 0], atEnd: false), "No height.");
        Assert.IsNull(CaretLocator.Edge([double.NaN, 20, 0, 15], atEnd: false));
        Assert.IsNull(CaretLocator.Edge([1e12, 20, 0, 15], atEnd: false));
    }

    [TestMethod]
    public void AnEmptyRangeIsFoundFromTheCharacterNextToIt()
    {
        var asked = new List<string>();
        double[]? Answer(string what, double[]? rectangles)
        {
            asked.Add(what);
            return rectangles;
        }

        // The range has bounds: nothing else is asked.
        Assert.AreEqual(Rect(40, 10, 41, 25), CaretLocator.CaretInRange(() => Answer("range", [40, 10, 0, 15]), () => Answer("next", [40, 10, 8, 15]), () => Answer("previous", [32, 10, 8, 15]), atEnd: false));
        CollectionAssert.AreEqual(RangeOnly, asked);

        // It has none: the caret is at the left edge of the character after it.
        asked.Clear();
        Assert.AreEqual(Rect(40, 10, 41, 25), CaretLocator.CaretInRange(() => Answer("range", []), () => Answer("next", [40, 10, 8, 15]), () => Answer("previous", [32, 10, 8, 15]), atEnd: false));
        CollectionAssert.AreEqual(RangeThenNext, asked);

        // At the end of the text there's none after it: the caret is at the right edge of the one before.
        asked.Clear();
        Assert.AreEqual(Rect(40, 10, 41, 25), CaretLocator.CaretInRange(() => Answer("range", []), () => Answer("next", null), () => Answer("previous", [32, 10, 8, 15]), atEnd: false));
        CollectionAssert.AreEqual(RangeNextPrevious, asked);

        Assert.IsNull(CaretLocator.CaretInRange(() => [], () => [], () => [], atEnd: false), "An empty document.");
    }

    [TestMethod]
    public void UiAutomationIsAskedOnAThreadOfItsOwnAndGivenUpOnWhenSlow()
    {
        var worker = new TimedWorker("Test worker", ApartmentState.MTA);
        Assert.IsTrue(worker.TryRun(() => (Thread: Environment.CurrentManagedThreadId, Apartment: Thread.CurrentThread.GetApartmentState()), 5_000, out var where));
        Assert.AreNotEqual(Environment.CurrentManagedThreadId, where.Thread, "Not the caller's thread.");
        Assert.AreEqual(ApartmentState.MTA, where.Apartment, "UI Automation's clients belong in the multithreaded apartment.");

        // An app that doesn't answer: the caller is let go after the timeout.
        using var hung = new ManualResetEventSlim();
        var clock = Stopwatch.StartNew();
        Assert.IsFalse(worker.TryRun(() => hung.Wait(30_000), 200, out _));
        Assert.IsTrue(clock.ElapsedMilliseconds < 2_000, $"Waited {clock.ElapsedMilliseconds} ms.");

        // While it's still stuck, more work isn't even started, so nothing piles up behind it.
        bool ran = false;
        clock.Restart();
        Assert.IsFalse(worker.TryRun(() => ran = true, 2_000, out _));
        Assert.IsTrue(clock.ElapsedMilliseconds < 500, $"Waited {clock.ElapsedMilliseconds} ms for a worker that's busy.");
        Assert.IsFalse(ran);

        // Once the app answers, the worker takes work again.
        hung.Set();
        bool answered = false;
        for (int attempt = 0; attempt < 50 && !answered; attempt++)
        {
            answered = worker.TryRun(() => 42, 1_000, out int value) && value == 42;
            if (!answered)
            {
                Thread.Sleep(50);
            }
        }

        Assert.IsTrue(answered);
        Assert.IsFalse(worker.TryRun<int>(() => throw new InvalidOperationException("The app went away."), 1_000, out _), "A query that fails is no answer.");
        Assert.IsTrue(worker.TryRun(() => 7, 1_000, out int next) && next == 7, "And doesn't stop the worker.");
    }

    private static CaretLocator.Sources Fakes(List<string> asked, Native.RECT? system, CaretLocator.Focus? automation, Native.RECT? window, Native.POINT? pointer = null, Func<bool>? inFront = null)
        => new(
            _ =>
            {
                asked.Add("system");
                return system;
            },
            _ =>
            {
                asked.Add("in front");
                return inFront?.Invoke() ?? true;
            },
            _ =>
            {
                asked.Add("automation");
                return automation;
            },
            _ =>
            {
                asked.Add("window");
                return window;
            },
            () =>
            {
                asked.Add("pointer");
                return pointer;
            },
            () => Rect(0, 0, 1920, 1040));

    private static Native.RECT Rect(int left, int top, int right, int bottom) => new() { Left = left, Top = top, Right = right, Bottom = bottom };
}
