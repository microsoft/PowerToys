// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InjectorDispatchTests
{
    [TestMethod]
    public void TypingSendsNewlinesAndTabsAsKeysAndASurrogatePairInOneCall()
    {
        // Real Enter and Tab presses, no \r, and the emoji's two halves together.
        using var box = new FakeTextBox(string.Empty);
        Assert.IsTrue(Replace(box, 0, "\r\n\tA😀", InjectionBackend.Typing, box.Dispatch));

        Assert.AreEqual("Mark | Enter | \t | A | Mark", string.Join(" | ", box.Batches.Where(batch => batch.Length != 4).Select(FakeTextBox.Describe)));
        Native.INPUT[] emoji = box.Batches.Single(batch => batch.Length == 4);
        Assert.AreEqual("😀", string.Concat(emoji.Where(input => (input.U.Ki.DwFlags & Native.KEYEVENTF_KEYUP) == 0).Select(input => (char)input.U.Ki.WScan)));
        Assert.IsTrue(box.Batches.SelectMany(batch => batch).All(input => input.U.Ki.DwExtraInfo == KeyboardHook.InjectionSignature), "Our own input, which the hook ignores.");
        Assert.AreEqual("\nA😀|", box.ToString());
    }

    [TestMethod]
    public void APartialSendEndsTheGuardAndNeverLeavesCtrlDown()
    {
        InjectionDiagnostics.Reset();
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        int calls = 0;

        // Nothing went out, not even the start marker: nothing to end or let go of.
        Assert.IsFalse(Replace(box, 2, "EXPANDED", InjectionBackend.Clipboard, _ =>
        {
            calls++;
            return 0;
        }));
        Assert.AreEqual(1, calls);
        Assert.IsFalse(box.Guard.Swallows(FakeTextBox.UserKey('z', up: false)));

        // Cut off after Ctrl and the V went down: Ctrl is let go of, and the end marker ends the guard.
        Assert.IsFalse(Replace(box, 2, "EXPANDED", InjectionBackend.Clipboard, inputs =>
        {
            Native.INPUT[] sent = inputs.Length == 2 && inputs[0].U.Ki.WVk == Native.VK_V ? inputs[..1] : inputs;
            _ = box.Dispatch(sent);
            return (uint)sent.Length;
        }));
        Assert.AreEqual("Mark | BS | BS | Ctrl-down | V-down | Ctrl-up | Mark", Calls(box));
        Assert.AreEqual("Before !aEXPANDED|", box.ToString());
        Assert.AreEqual("the user's", box.Clipboard);
        Assert.IsFalse(box.Guard.Swallows(FakeTextBox.UserKey('z', up: false)));
        Assert.AreEqual(2, InjectionDiagnostics.SendInputFailures);

        // Ctrl's key-up cut off: sent once more, and the paste doesn't count.
        using var cut = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        int ctrlUps = 0;
        Assert.IsFalse(Replace(cut, 2, "EXPANDED", InjectionBackend.Clipboard, inputs =>
        {
            bool ctrlUp = inputs.Length == 1 && inputs[0].U.Ki.WVk == Native.VK_CONTROL && (inputs[0].U.Ki.DwFlags & Native.KEYEVENTF_KEYUP) != 0;
            return ctrlUp && ctrlUps++ == 0 ? 0 : cut.Dispatch(inputs);
        }));
        Assert.AreEqual(2, ctrlUps);
        Assert.AreEqual("Mark | BS | BS | Ctrl-down | V | Ctrl-up | Mark", Calls(cut));
        Assert.AreEqual(3, InjectionDiagnostics.SendInputFailures);
        Assert.IsFalse(cut.Guard.Swallows(FakeTextBox.UserKey('z', up: false)));
        InjectionDiagnostics.Reset();
    }

    [TestMethod]
    public void ATargetStillBusyWithTheEraseGetsThePasteRatherThanAV()
    {
        // Windows 11 Notepad, still applying the Backspaces when the paste's V arrived, read Ctrl as it was once it got to
        // the V. Sent with the V in one call, Ctrl had gone up by then, and a "v" went in instead of the snippet. Ctrl stays
        // down until the target is seen reading the clipboard.
        using var box = new FakeTextBox("Before !abc") { Late = true, Clipboard = "the user's" };
        Assert.IsTrue(Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, box.Dispatch, sleep: _ => box.Pump()));
        box.Pump();
        Assert.AreEqual("Before EXPANDED |", box.ToString());
        Assert.AreEqual("Mark | BS | BS | BS | BS | Ctrl-down | V | Ctrl-up | Mark", Calls(box));
    }

    [TestMethod]
    public void CtrlIsLetGoOfWhenTheClipboardWouldGoBackIfTheReadIsNeverSeen()
    {
        // Windows 11 Notepad holds the clipboard open for well under a millisecond, so its read is seldom seen. Ctrl stays
        // down for as long as the snippet waits on the clipboard for that read, counted from the V.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        long now = 0;
        var sentAt = new List<long>();
        box.BeforeCall = _ => sentAt.Add(now);
        Assert.IsTrue(Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, box.Dispatch, sleep: ms => now += ms, isOpenByTarget: () => false, clock: () => now));
        Assert.AreEqual("Mark | BS | BS | BS | BS | Ctrl-down | V | Ctrl-up | Mark", Calls(box));
        int v = CallOf(box, "V");
        Assert.AreEqual(Injector.PasteSettleMs, sentAt[v + 1] - sentAt[v], "From the V to Ctrl's key-up.");
        Assert.AreEqual("Before EXPANDED |", box.ToString());
    }

    [TestMethod]
    public void ThePasteWaitsAfterTheEraseBeforeCtrlGoesDown()
    {
        // Windows 11 Notepad keeps working on an edit for a while after it, and dropped a Ctrl+V that came meanwhile, pasting
        // nothing.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        long now = 0;
        var sentAt = new List<long>();
        box.BeforeCall = _ => sentAt.Add(now);
        Assert.IsTrue(Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, box.Dispatch, sleep: ms => now += ms, clock: () => now));
        Assert.AreEqual("Mark | BS | BS | BS | BS | Ctrl-down | V | Ctrl-up | Mark", Calls(box));
        int ctrl = CallOf(box, "Ctrl-down");
        Assert.AreEqual(Injector.PrePasteDelayMs, sentAt[ctrl] - sentAt[ctrl - 1], "From the erase to Ctrl.");
        Assert.AreEqual(0, sentAt[ctrl + 1] - sentAt[ctrl], "Ctrl and the V together.");
        Assert.AreEqual("Before EXPANDED |", box.ToString());
    }

    [TestMethod]
    public void TheEraseGoesOutAKeyAtATimePacedLikeTyping()
    {
        // Windows 11 Notepad dropped a Backspace now and then from an erase sent in one call, leaving the trigger's first
        // character in front of the snippet. The modifiers the user holds are let go of in the first call.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        long now = 0;
        var sentAt = new List<long>();
        box.BeforeCall = _ => sentAt.Add(now);
        Assert.IsTrue(Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, box.Dispatch, sleep: ms => now += ms, clock: () => now, keyState: vk => vk == Native.VK_LSHIFT ? unchecked((short)0x8000) : (short)0));
        StringAssert.StartsWith(Calls(box), "Mark | LShift-up BS | BS | BS | BS | Ctrl-down | V | Ctrl-up | Mark");
        Assert.AreEqual($"{Injector.KeyDelayMs} {Injector.KeyDelayMs} {Injector.KeyDelayMs}", string.Join(" ", Enumerable.Range(2, 3).Select(i => sentAt[i] - sentAt[i - 1])), "A key's pause apart.");
        Assert.AreEqual("Before EXPANDED |", box.ToString());
    }

    [TestMethod]
    public void AnEraseLongerThanThePacedMostGoesOutAtOnce()
    {
        // Undoing a long snippet: paced, the erase would hold the user's keys for seconds.
        string erased = new('x', Injector.MaxPacedEraseKeys + 1);
        using var box = new FakeTextBox("Before " + erased);
        var sleeps = new List<int>();
        Assert.IsTrue(Replace(box, erased.Length, "ab", InjectionBackend.Typing, box.Dispatch, sleep: sleeps.Add));
        Assert.AreEqual($"Mark | {string.Join(" ", Enumerable.Repeat("BS", erased.Length))} | a | b | Mark", Calls(box));
        Assert.AreEqual($"{Injector.PreBackspaceDelayMs} {Injector.KeyDelayMs} {Injector.KeyDelayMs}", string.Join(" ", sleeps), "No pause inside the erase: only before it, and after each typed character.");
        Assert.AreEqual("Before ab|", box.ToString());
    }

    [TestMethod]
    public void CtrlIsLetGoOfAtOnceWhenTheWindowLosesTheFront()
    {
        // Once another window is in front, the guard drops what it held and the user's next keys go there: with Ctrl still
        // down, they would go in as shortcuts.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        IntPtr front = new(1);
        var sleeps = new List<int>();
        box.AfterKey = key =>
        {
            if (key.VkCode == Native.VK_V)
            {
                front = new IntPtr(2);
            }
        };

        _ = Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, box.Dispatch, sleep: sleeps.Add, isOpenByTarget: () => false, foreground: () => front);
        Assert.AreEqual("Mark | BS | BS | BS | BS | Ctrl-down | V | Ctrl-up | Mark", Calls(box));
        Assert.AreEqual(Pauses(4), string.Join(" ", sleeps), "Only the pauses before and inside the erase and before the paste: no wait with Ctrl down.");
    }

    [TestMethod]
    public void AKeyTypedWhileCtrlIsDownGoesInAfterThePasteNotAsAShortcut()
    {
        // The paste once went out in separate calls, and a key typed between them became Ctrl+A or Ctrl+S. Ctrl now stays
        // down for a while, so only while the guard holds the user's keys: they go in after the paste, as typed.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        bool typed = false;
        Assert.IsTrue(Replace(
            box,
            4,
            "EXPANDED ",
            InjectionBackend.Clipboard,
            box.Dispatch,
            sleep: _ =>
            {
                if (!typed && box.Batches.Count > 0 && FakeTextBox.Describe(box.Batches[^1]) == "V")
                {
                    typed = true;
                    box.UserTypes("a");
                }
            },
            isOpenByTarget: () => typed));
        Assert.AreEqual("Mark | BS | BS | BS | BS | Ctrl-down | V | Ctrl-up | Mark | a Mark", Calls(box));
        Assert.AreEqual("Before EXPANDED a|", box.ToString());
    }

    [TestMethod]
    public void AnInsertionGoingInUnguardedDoesntHoldCtrl()
    {
        // Another hook swallowed the markers before ours saw them, so nothing holds the user's keys: the chord goes out in
        // one call, as before, rather than keep Ctrl down for them to go in with.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        box.Guard.MarkerWaitMs = 0;
        var sleeps = new List<int>();
        uint WithoutMarkers(Native.INPUT[] inputs)
        {
            _ = box.Dispatch([.. inputs.Where(input => input.U.Ki.WVk != InsertionGuard.MarkerVk)]);
            return (uint)inputs.Length;
        }

        Assert.IsTrue(Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, WithoutMarkers, sleep: sleeps.Add, isOpenByTarget: () => false));
        Assert.AreEqual("BS | BS | BS | BS | Ctrl-down V Ctrl-up", string.Join(" | ", box.Batches.Where(batch => batch.Length > 0).Select(FakeTextBox.Describe)));
        Assert.AreEqual(Pauses(4), string.Join(" ", sleeps), "No wait with Ctrl down.");
        Assert.AreEqual("Before EXPANDED |", box.ToString());
    }

    [TestMethod]
    public void AClickBeforeTheVStopsThePaste()
    {
        // The V goes out in a call of its own, so like every other call it waits on a check: a click, which isn't held,
        // may have moved the caret or focus, and the paste would land there. Ctrl is let go of.
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        box.BeforeCall = calls =>
        {
            // The start marker and the four Backspaces are in: the click comes with Ctrl.
            if (calls == 5)
            {
                box.Hook.Click();
            }
        };

        Assert.IsFalse(Replace(box, 4, "EXPANDED ", InjectionBackend.Clipboard, box.Dispatch));
        Assert.AreEqual("Mark | BS | BS | BS | BS | Ctrl-down | Ctrl-up | Mark", Calls(box));
        Assert.AreEqual("Before |", box.ToString());
        Assert.AreEqual("the user's", box.Clipboard);
    }

    [TestMethod]
    public void AFailureAfterEraseIsTerminalAndCannotRetainUndo()
    {
        InjectionBackend originalBackend = Injector.Backend;
        using var guard = new InsertionGuard(() => 0, _ => true) { MarkerWaitMs = 0 };
        bool recorded = false;
        try
        {
            InjectionDiagnostics.Reset();
            Injector.Backend = InjectionBackend.Typing;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                2,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                dispatch: inputs => inputs.Length > 1 ? (uint)inputs.Length - 1 : (uint)inputs.Length,
                getAsyncKeyState: _ => 0,
                sleep: _ => { },
                inserted: () => recorded = true));
            Assert.AreEqual(0, InjectionDiagnostics.Typed);

            Injector.Backend = InjectionBackend.Clipboard;
            bool typedFallback = false;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                0,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                (_, _, _, _) => PasteOutcome.FailedAfterErase,
                inputs =>
                {
                    typedFallback |= inputs[0].U.Ki.WVk != InsertionGuard.MarkerVk;
                    return (uint)inputs.Length;
                },
                inserted: () => recorded = true));
            Assert.IsFalse(typedFallback);
            Assert.AreEqual(0, InjectionDiagnostics.Typed);
            Assert.AreEqual(1, InjectionDiagnostics.Abandoned);
            Assert.IsFalse(recorded, "No undo record for text that may not be there.");
        }
        finally
        {
            InjectionDiagnostics.Reset();
            Injector.Backend = originalBackend;
        }
    }

    [TestMethod]
    public void ATypedEnterCutOffPartWayIsLetGoOf()
    {
        // Cut off part way, a typed Enter's key-up may not have gone, and Enter stayed down.
        InjectionBackend originalBackend = Injector.Backend;
        using var guard = new InsertionGuard(() => 0, _ => true) { MarkerWaitMs = 0 };
        var batches = new List<Native.INPUT[]>();
        try
        {
            Injector.Backend = InjectionBackend.Typing;
            Assert.IsFalse(Injector.ReplaceCore(
                0,
                "a\nb",
                IntPtr.Zero,
                0,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                dispatch: inputs =>
                {
                    batches.Add(inputs);
                    bool enterDown = inputs.Any(input => input.U.Ki.WVk == Native.VK_RETURN && (input.U.Ki.DwFlags & Native.KEYEVENTF_KEYUP) == 0);
                    return enterDown ? (uint)inputs.Length - 1 : (uint)inputs.Length;
                },
                getAsyncKeyState: _ => 0,
                sleep: _ => { },
                isWindowsTerminal: _ => false,
                isConsole: _ => false));

            int cutOff = batches.FindIndex(batch => batch.Any(input => input.U.Ki.WVk == Native.VK_RETURN));
            Native.INPUT[] next = batches[cutOff + 1];
            Assert.AreEqual(1, next.Length);
            Assert.AreEqual(Native.VK_RETURN, (int)next[0].U.Ki.WVk);
            Assert.AreNotEqual(0u, next[0].U.Ki.DwFlags & Native.KEYEVENTF_KEYUP);
        }
        finally
        {
            Injector.Backend = originalBackend;
        }
    }

    [TestMethod]
    public void ASurrogatePairTypedIntoATerminalStaysOneBatch()
    {
        // With a control character left out in front of it, the pair was looked for in the text as written, one place on,
        // and its halves went in apart.
        InjectionBackend originalBackend = Injector.Backend;
        using var guard = new InsertionGuard(() => 0, _ => true) { MarkerWaitMs = 0 };
        var batches = new List<Native.INPUT[]>();
        try
        {
            Injector.Backend = InjectionBackend.Typing;
            Assert.IsTrue(Injector.ReplaceCore(
                0,
                "\u000F\U0001F642",
                IntPtr.Zero,
                0,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                dispatch: inputs =>
                {
                    batches.Add(inputs);
                    return (uint)inputs.Length;
                },
                getAsyncKeyState: _ => 0,
                sleep: _ => { },
                isWindowsTerminal: _ => false,
                isConsole: _ => true));

            Native.INPUT[] pair = batches.Single(batch => batch.Any(input => (input.U.Ki.DwFlags & Native.KEYEVENTF_UNICODE) != 0));
            CollectionAssert.AreEqual(new ushort[] { 0xD83D, 0xD83D, 0xDE42, 0xDE42 }, pair.Select(input => input.U.Ki.WScan).ToArray());
        }
        finally
        {
            Injector.Backend = originalBackend;
        }
    }

    [TestMethod]
    public void ASlowClipboardLossHandlerDoesntHoldUpTheWorker()
    {
        // Its message box, opened on the worker, could sit behind other windows: until someone closed it, every trigger
        // swallowed its space and inserted nothing.
        Action<string>? original = Injector.ClipboardRestoreFailed;
        var shown = new TaskCompletionSource();
        var closed = new TaskCompletionSource();
        try
        {
            Injector.ClipboardRestoreFailed = _ =>
            {
                shown.TrySetResult();
                closed.Task.Wait();
            };
            Assert.IsTrue(Task.Run(Injector.ReportClipboardLoss).Wait(10_000), "The worker waited on the message box.");
            Assert.IsTrue(shown.Task.Wait(10_000), "Reported all the same.");
        }
        finally
        {
            closed.TrySetResult();
            Injector.ClipboardRestoreFailed = original;
        }
    }

    [TestMethod]
    public void APasteHasCrLfLineBreaksOnTheClipboard()
    {
        // The console ran two lines pasted with a lone LF between them as one. A lone CR, a control character, stays.
        using var box = new FakeTextBox("!lines");
        var pasted = new List<string>();
        InjectionBackend original = Injector.Backend;
        try
        {
            Injector.Backend = InjectionBackend.Clipboard;
            Assert.IsTrue(Injector.ReplaceCore(
                6,
                "echo one\necho two\r\nthree\rfour ",
                IntPtr.Zero,
                0,
                _ => true,
                box.Guard,
                box.Hook.InputGeneration,
                () => new IntPtr(1),
                (text, window, send, keepAlive) =>
                {
                    pasted.Add(text);
                    return box.Paste(text, window, send, keepAlive);
                },
                box.Dispatch,
                getAsyncKeyState: _ => 0,
                sleep: _ => { },
                isOpenByTarget: box.TookPaste,
                givenBack: 1));
        }
        finally
        {
            Injector.Backend = original;
        }

        Assert.AreEqual("echo one\r\necho two\r\nthree\rfour ", pasted.Single());
        Assert.AreEqual("echo one\necho two\nthree\rfour |", box.ToString());
    }

    private static string Calls(FakeTextBox box) => string.Join(" | ", box.Batches.Select(FakeTextBox.Describe));

    /// <summary>The injector's pauses for an erase of <paramref name="erased"/> keys and a paste: before the erase, between its keys, and before Ctrl.</summary>
    private static string Pauses(int erased) => string.Join(" ", Enumerable.Repeat(Injector.KeyDelayMs, erased - 1).Prepend(Injector.PreBackspaceDelayMs).Append(Injector.PrePasteDelayMs));

    /// <summary>The index of the first call that sent exactly <paramref name="description"/>.</summary>
    private static int CallOf(FakeTextBox box, string description) => box.Batches.FindIndex(batch => FakeTextBox.Describe(batch) == description);

    private static bool Replace(FakeTextBox box, int erase, string text, InjectionBackend backend, Func<Native.INPUT[], uint> dispatch, Action<int>? sleep = null, Func<bool>? isOpenByTarget = null, Func<long>? clock = null, Func<IntPtr>? foreground = null, Func<int, short>? keyState = null)
    {
        InjectionBackend original = Injector.Backend;
        try
        {
            box.Batches.Clear();
            Injector.Backend = backend;
            return Injector.ReplaceCore(erase, text, IntPtr.Zero, 0, _ => true, box.Guard, box.Hook.InputGeneration, foreground ?? (() => new IntPtr(1)), box.Paste, dispatch, getAsyncKeyState: keyState ?? (_ => 0), sleep: sleep ?? (_ => { }), isOpenByTarget: isOpenByTarget ?? box.TookPaste, clock: clock);
        }
        finally
        {
            Injector.Backend = original;
        }
    }
}
