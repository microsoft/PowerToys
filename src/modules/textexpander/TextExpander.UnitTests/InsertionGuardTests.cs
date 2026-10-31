// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InsertionGuardTests
{
    private static readonly IntPtr Target = new(10);

    [TestMethod]
    public void MarkersAreAlwaysSwallowedAndOnlyTheInsertionsOwnCount()
    {
        using var guard = Guard(() => 1, []);

        // With no insertion being sent, a marker can only be a late one.
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(7)));
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('a', up: false)));

        // The 0xFF key-up the runner sends so Start doesn't open isn't ours.
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserVk(InsertionGuard.MarkerVk, up: true) with { DwExtraInfo = HostInputTags.CentralizedHookDontTrigger }));

        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(0)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker((byte)(id + 1))));
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('a', up: false)), "Neither is this insertion's.");
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('b', up: false)));
        Assert.IsTrue(guard.AwaitStart());
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        guard.AwaitEnd(markerSent: true, []);
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('c', up: false)), "Over once what it held is back.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FromTheStartMarkerOtherKeysWaitAndGoBackAfterTheInsertion(bool keyFirst)
    {
        // Whether or not the insertion goes ahead (a key reached the app first), keys typed from the start marker on
        // go back after the end marker, in order.
        long generation = 5;
        var sent = new List<Native.INPUT[]>();
        using var guard = Guard(() => generation, sent);
        byte id = guard.Begin(generation, Target);
        if (keyFirst)
        {
            generation++;
        }

        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.AreEqual(!keyFirst, guard.AwaitStart());
        Assert.IsFalse(guard.Swallows(OurKey(Native.VK_BACK, up: false)), "The insertion's own keys go through.");
        Assert.IsTrue("ab".All(ch => guard.Swallows(FakeTextBox.UserKey(ch, up: false)) && guard.Swallows(FakeTextBox.UserKey(ch, up: true))));
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.AreEqual(0, sent.Count, "The hook only holds them: the worker sends them back.");
        guard.AwaitEnd(markerSent: true, []);
        Assert.AreEqual("a b Mark", FakeTextBox.Describe(sent.Single()), "They come through, and the marker after them ends the guard.");
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('c', up: false)));
    }

    [TestMethod]
    public void HeldKeysOnlyGoBackToTheirOwnWindow()
    {
        var sent = new List<Native.INPUT[]>();
        bool inFront = true;
        using var guard = Guard(() => 1, sent, _ => inFront);

        // Another window comes to the front: what's held is dropped, and the insertion stays cancelled.
        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('a', up: false)));
        guard.ForegroundChanged(Target);
        Assert.IsFalse(guard.Cancelled, "Its own window coming back changes nothing.");
        guard.ForegroundChanged(new IntPtr(11));
        Assert.IsTrue(guard.Cancelled);
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('a', up: true)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));

        // Not in front at the end marker: dropped too.
        id = guard.Begin(1, Target);
        Assert.IsFalse(guard.Cancelled);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('b', up: false)));
        inFront = false;
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        guard.AwaitEnd(markerSent: true, []);
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('b', up: true)));
        Assert.AreEqual(0, sent.Count);
    }

    [TestMethod]
    public void InputALowerIntegrityAppInjectedIsNeverHeld()
    {
        // Sent back, it would carry our integrity level.
        var sent = new List<Native.INPUT[]>();
        using var guard = Guard(() => 1, sent);
        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('a', up: false) with { Flags = Native.LLKHF_LOWER_IL_INJECTED | 0x10 }));
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        guard.AwaitEnd(markerSent: true, []);
        Assert.AreEqual(0, sent.Count);
    }

    [TestMethod]
    public void TheGuardEndsByItselfWhenTheInsertionStalls()
    {
        long now = 0;
        var sent = new List<Native.INPUT[]>();
        using var guard = Guard(() => 1, sent);
        guard.Clock = () => now;
        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('x', up: false)));

        // Each event of ours starts the wait again.
        now = InsertionGuard.TimeoutMs - 1;
        Assert.IsFalse(guard.Swallows(OurKey(Native.VK_BACK, up: false)));
        now = InsertionGuard.TimeoutMs;
        guard.Expire();
        Assert.AreEqual(0, sent.Count);

        now = (2 * InsertionGuard.TimeoutMs) - 1;
        guard.Expire();
        Assert.AreEqual("x", FakeTextBox.Describe(sent.Single()), "Sent back, and let go of.");
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('y', up: false)), "The user's keys pass again.");

        // The worker carries on: its end marker is swallowed, and it doesn't wait.
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        guard.AwaitEnd(markerSent: true, []);
        Assert.AreEqual(1, sent.Count);
    }

    [TestMethod]
    public void TooManyHeldKeysEndTheGuard()
    {
        // The timer ends it at once and sends them back: the hook never sends.
        var sent = new TaskCompletionSource<int>();
        using var guard = new InsertionGuard(() => 1, _ => true)
        {
            Resend = inputs =>
            {
                sent.TrySetResult(inputs.Length);
                return (uint)inputs.Length;
            },
        };
        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        for (int i = 0; i < InsertionGuard.MaxHeld; i++)
        {
            Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('a', up: (i & 1) != 0)));
        }

        Assert.IsTrue(sent.Task.Wait(10_000));
        Assert.AreEqual(InsertionGuard.MaxHeld, sent.Task.Result);
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('b', up: false)));
    }

    [TestMethod]
    public void HeldKeysGoBackAsTheyCameButForTheInjectedFlag()
    {
        // A key Keyboard Manager remapped (0x11) keeps its tag, so it isn't remapped a second time, and so does the
        // runner's replay (0x110). The user's own typing stays untagged. The injected flag is SendInput's to set.
        var sent = new List<Native.INPUT[]>();
        using var guard = Guard(() => 1, sent);
        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        var home = new Native.KBDLLHOOKSTRUCT { VkCode = 0x24, ScanCode = 0x47, Flags = Native.LLKHF_EXTENDED | 0x10, DwExtraInfo = 0x11 };
        Native.KBDLLHOOKSTRUCT[] keys =
        [
            home,
            home with { Flags = home.Flags | Native.LLKHF_UP },
            new() { VkCode = 0x41, ScanCode = 0x1E, Flags = 0x10, DwExtraInfo = HostInputTags.CentralizedHookDontTrigger },
            new() { VkCode = 0x41, ScanCode = 0x1E, Flags = 0x10 | Native.LLKHF_UP, DwExtraInfo = HostInputTags.CentralizedHookDontTrigger },
            FakeTextBox.UserKey('é', up: false),
            FakeTextBox.UserKey('é', up: true),
        ];
        Assert.IsTrue(keys.All(guard.Swallows));
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        guard.AwaitEnd(markerSent: true, []);

        Native.KEYBDINPUT[] resend = [.. sent.Single().SkipLast(1).Select(input => input.U.Ki)];
        Assert.AreEqual(6, resend.Length);
        Assert.AreEqual((ushort)0x24, resend[0].WVk);
        Assert.AreEqual((ushort)0x47, resend[0].WScan);
        Assert.AreEqual(Native.KEYEVENTF_EXTENDEDKEY, resend[0].DwFlags);
        Assert.AreEqual(Native.KEYEVENTF_EXTENDEDKEY | Native.KEYEVENTF_KEYUP, resend[1].DwFlags);
        Assert.AreEqual(new IntPtr(0x11), resend[1].DwExtraInfo);
        Assert.AreEqual(0u, resend[2].DwFlags);
        Assert.AreEqual(new IntPtr(HostInputTags.CentralizedHookDontTrigger), resend[3].DwExtraInfo);
        Assert.AreEqual((ushort)0, resend[4].WVk);
        Assert.AreEqual((ushort)'é', resend[4].WScan);
        Assert.AreEqual(Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP, resend[5].DwFlags);
        Assert.AreEqual(IntPtr.Zero, resend[5].DwExtraInfo);
    }

    [TestMethod]
    public void WithoutItsMarkersAKeyStillDownGetsAKeyUpAndAModifierStillDownIsDropped()
    {
        Native.INPUT[] resend = InsertionGuard.ToResend(
        [
            FakeTextBox.UserVk(Native.VK_LSHIFT, up: false), // Shift+A, both let go of in time: a capital A
            FakeTextBox.UserVk(0x41, up: false),
            FakeTextBox.UserVk(0x41, up: true),
            FakeTextBox.UserVk(Native.VK_LSHIFT, up: true),
            FakeTextBox.UserVk(0x42, up: true),              // B's key-up: it went down before the insertion
            FakeTextBox.UserVk(0x43, up: false),             // C, held down and repeating
            FakeTextBox.UserVk(0x43, up: false),
            FakeTextBox.UserVk(Native.VK_RSHIFT, up: false), // Shift, still down when the guard ended
        ]);

        // C is let go of after. The Shift still down is dropped: if its real key-up has gone by, it would stay down.
        Assert.AreEqual("LShift-down A LShift-up B-up C-down C", FakeTextBox.Describe(resend));
    }

    [TestMethod]
    public void AnAcceptedPromptAnswerHoldsKeysForTheInsertion()
    {
        // Typed at once after Enter, the first letter was lost, and the expansion with it. Held from the answer on,
        // it goes in after the expansion; the prompt going and its window coming back change nothing.
        var sent = new List<Native.INPUT[]>();
        bool inFront = true;
        using var guard = Guard(() => 3, sent, _ => inFront);
        guard.Hold(Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('o', up: false)));
        guard.ForegroundChanged(new IntPtr(11));
        guard.ForegroundChanged(Target);

        byte id = guard.Begin(3, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsTrue(guard.AwaitStart(), "Nothing reached the app.");
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        guard.AwaitEnd(markerSent: true, []);
        Assert.AreEqual("o-down Mark", FakeTextBox.Describe(sent.Single()));

        // If the window doesn't come back, the work ends without inserting, and what's held is dropped.
        guard.Hold(Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('p', up: false)));
        inFront = false;
        guard.Abandon();
        Assert.AreEqual(1, sent.Count);

        // Ended before the start marker came, as when the hold runs out, or no marker while keys are held: what was held
        // goes back where the erase would take it, so the insertion doesn't go ahead.
        inFront = true;
        guard.MarkerWaitMs = 0;
        guard.Hold(Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('q', up: false)));
        guard.Begin(3, Target);
        guard.Abandon();
        Assert.IsFalse(guard.AwaitStart());
        guard.Hold(Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('r', up: false)));
        guard.Begin(3, Target);
        Assert.IsFalse(guard.AwaitStart());
        Assert.AreEqual("q | r", string.Join(" | ", sent.Skip(1).Select(FakeTextBox.Describe)));
    }

    [TestMethod]
    public void DisposingTheGuardLetsTheWorkerGo()
    {
        using var guard = Guard(() => 1, []);
        guard.MarkerWaitMs = 60_000;
        byte id = guard.Begin(1, Target);
        Assert.IsTrue(guard.Swallows(FakeTextBox.Marker(id)));
        Assert.IsTrue(guard.Swallows(FakeTextBox.UserKey('a', up: false)));
        Task worker = Task.Run(() => guard.AwaitEnd(markerSent: true, []));
        guard.Dispose();
        Assert.IsTrue(worker.Wait(10_000));
        Assert.IsFalse(guard.Swallows(FakeTextBox.UserKey('b', up: false)));
    }

    [TestMethod]
    public void OurTagIsKeyboardManagersBitAndClearOfTheOtherTags()
    {
        long tag = KeyboardHook.InjectionSignature.ToInt64();
        Assert.AreEqual(HostInputTags.KeyboardManagerInjected, tag & 0xFFF);
        Assert.IsTrue(tag >= 0x0100_0000);
        Assert.AreEqual(HostInputKind.Ignored, HostInputTags.Classify(KeyboardHook.InjectionSignature, KeyboardHook.InjectionSignature));
    }

    [TestMethod]
    public void ADiscardSendsOnlyItsMarkersAndDoesntBorrowTheClipboard()
    {
        // The user pressed Ctrl+V before the start marker reached the hook: their paste went in and the expansion stayed
        // out. With the clipboard borrowed first, it pasted the snippet, or nothing. The space that completed the trigger
        // isn't typed back.
        using var box = new FakeTextBox(";email") { Clipboard = "the user's" };
        box.BeforeCall = calls =>
        {
            if (calls == 0)
            {
                box.UserPresses((Native.VK_LCONTROL, false), (Native.VK_V, false), (Native.VK_V, true), (Native.VK_LCONTROL, true));
            }
        };

        Assert.IsFalse(Run(box, 6, "alex@contoso.com ", backend: InjectionBackend.Clipboard));
        Assert.AreEqual(";emailthe user's|", box.ToString());
        Assert.AreEqual("the user's", box.Clipboard);
        Assert.AreEqual("Mark | Mark", Calls(box));
    }

    [TestMethod]
    public void HeldKeysGoBackAfterTheUndoRecordAndTheUsersClipboard()
    {
        // A Ctrl+V right after a pasted expansion pasted the snippet again, and a Ctrl+Z ran the app's own undo: the
        // guard let them go before the clipboard went back and the undo record was kept.
        using var box = new FakeTextBox(";email") { Clipboard = "the user's" };
        box.BeforeCall = calls =>
        {
            if (calls == 1)
            {
                box.UserPresses((Native.VK_LCONTROL, false), (Native.VK_V, false), (Native.VK_V, true), (Native.VK_LCONTROL, true));
            }
        };

        int recordedAt = -1;
        Assert.IsTrue(Run(box, 6, "alex@contoso.com ", backend: InjectionBackend.Clipboard, inserted: () => recordedAt = box.Batches.Count));
        Assert.AreEqual("Mark | BS BS BS BS BS BS | Ctrl-down V Ctrl-up | Mark | LCtrl-down V LCtrl-up Mark", Calls(box));
        Assert.AreEqual(3, recordedAt, "Kept before the end marker.");
        Assert.AreEqual("alex@contoso.com the user's|", box.ToString());
    }

    [TestMethod]
    public void ModifiersHeldAtTheTriggerArePressedAgainBeforeTheHeldKeys()
    {
        // Shift held from typing the trigger is let go of for the erase. A capital typed meanwhile came out lowercase,
        // and Ctrl+Z, with Ctrl still down at the end, a "z": Shift is pressed again, and Ctrl comes back with Z. An "x"
        // typed while they go back waits for them: with Ctrl down, it would have been Ctrl+X.
        using var box = new FakeTextBox("!d");
        box.BeforeCall = calls =>
        {
            if (calls == 2)
            {
                box.UserPresses((0x48, false), (0x48, true), (Native.VK_LCONTROL, false), (0x5A, false), (0x5A, true));
            }
        };

        box.AfterKey = key =>
        {
            if (key.VkCode == Native.VK_LCONTROL && box.Batches.Count == 6 && !box.Text.Contains('x'))
            {
                box.UserTypes("x");
            }
        };

        Assert.IsTrue(Run(box, 2, "ab", keys: vk => vk == Native.VK_LSHIFT ? unchecked((short)0x8000) : (short)0));
        Assert.AreEqual("Mark | LShift-up BS BS | a | b | Mark | LShift-down H LCtrl-down Z Mark | x Mark", Calls(box));
        Assert.AreEqual("abx|", box.ToString());
    }

    [TestMethod]
    public void WhatKeyboardManagerSendsInPlaceOfAKeyGoingBackIsntHeld()
    {
        // Our hook runs before Keyboard Manager's. Caps Lock remapped to Ctrl went back, and came through as an injected
        // Ctrl the guard held, so Caps+V reached the app as V alone.
        using var box = new FakeTextBox("!d") { Clipboard = "copied", Remap = (Native.VK_CAPITAL, Native.VK_LCONTROL) };
        box.BeforeCall = calls =>
        {
            if (calls == 1)
            {
                box.UserPresses((Native.VK_CAPITAL, false), (Native.VK_V, false), (Native.VK_V, true), (Native.VK_CAPITAL, true));
            }
        };

        Assert.IsTrue(Run(box, 2, "ab"));
        Assert.AreEqual("abcopied|", box.ToString());
    }

    [TestMethod]
    public void AnInjectedKeyUpWaitsForItsKeyDownGoingBack()
    {
        // The Sandbox types by injecting, as the touch keyboard and AutoHotkey do. P went down during the insertion and
        // came up while it went back: let through, the key-up overtook the key-down, and P stayed down.
        using var box = new FakeTextBox("!d");
        var p = new Native.KBDLLHOOKSTRUCT { VkCode = 0x50, Flags = 0x10 };
        string order = string.Empty;
        box.AfterKey = key => order += key.VkCode == 0x50 ? ((key.Flags & Native.LLKHF_UP) != 0 ? "↑" : "↓") : string.Empty;
        box.BeforeCall = calls =>
        {
            if (calls is 1 or 5)
            {
                box.Take(calls == 1 ? p : p with { Flags = p.Flags | Native.LLKHF_UP });
            }
        };

        Assert.IsTrue(Run(box, 2, "ab"));
        Assert.AreEqual("Mark | BS BS | a | b | Mark | P-down Mark | P-up Mark", Calls(box));
        Assert.AreEqual("↓↑", order, "Nothing is left down.");
    }

    [TestMethod]
    public void TheClipboardIsBorrowedAfterTheDecision()
    {
        // So a refused paste is typed inside the same guard, and a key typed meanwhile goes in after it.
        using var box = new FakeTextBox("!d");
        Assert.IsTrue(Run(box, 2, "ab", backend: InjectionBackend.Clipboard, paste: (_, _, _) =>
        {
            box.UserTypes("u");
            return PasteOutcome.RefusedBeforeErase;
        }));
        Assert.AreEqual("Mark | BS BS | a | b | Mark | u Mark", Calls(box));
        Assert.AreEqual("abu|", box.ToString());

        // A click while it's borrowed isn't held, and may have moved the caret: nothing is erased.
        box.Batches.Clear();
        Assert.IsFalse(Run(box, 2, "cd", backend: InjectionBackend.Clipboard, paste: (text, target, send) =>
        {
            box.Hook.ClearUndo();
            return box.Paste(text, target, send);
        }));
        Assert.AreEqual("Mark | Mark", Calls(box));
        Assert.AreEqual("abu|", box.ToString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AGuardThatRanOutDuringTheBorrowDecidesAgainBeforeTheErase(bool refused)
    {
        // Excel delay-renders a large clipboard: copying it outlasted the guard, a Ctrl pressed meanwhile reached the
        // app, and the Backspaces went in as Ctrl+Backspace. A new start marker decides again, and Ctrl is let go of
        // first. A refused paste, typed instead, goes the same way.
        long now = 0;
        bool ctrl = false;
        using var box = new FakeTextBox("!d");
        box.Guard.Clock = () => now;
        Assert.IsTrue(Run(box, 2, "ab", backend: InjectionBackend.Clipboard, keys: vk => ctrl && vk == Native.VK_LCONTROL ? unchecked((short)0x8000) : (short)0, paste: (text, target, send) =>
        {
            now = InsertionGuard.TimeoutMs;
            box.Guard.Expire();

            // Ctrl, pressed now, reaches the app, and as a modifier doesn't move the input generation.
            ctrl = true;
            Assert.IsFalse(box.Hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
            return refused ? PasteOutcome.RefusedBeforeErase : box.Paste(text, target, send);
        }));
        Assert.AreEqual($"Mark | Mark | LCtrl-up BS BS | {(refused ? "a | b" : "Ctrl-down V Ctrl-up")} | Mark | LCtrl-down Mark", Calls(box));
        Assert.AreEqual("ab|", box.ToString());
    }

    [TestMethod]
    public void WithNoMarkerSeenTheGuardEndsAndTheInsertionCounts()
    {
        // Another hook swallowed the markers before ours saw them: the insertion goes in unguarded, and the worker
        // doesn't wait for ever.
        using var box = new FakeTextBox(";email");
        box.Guard.MarkerWaitMs = 0;
        var markers = new List<Native.INPUT>();
        uint WithoutMarkers(Native.INPUT[] inputs)
        {
            markers.AddRange(inputs.Where(input => input.U.Ki.WVk == InsertionGuard.MarkerVk));
            _ = box.Dispatch([.. inputs.Where(input => input.U.Ki.WVk != InsertionGuard.MarkerVk)]);
            return (uint)inputs.Length;
        }

        Assert.IsTrue(Run(box, 6, "alex@contoso.com", dispatch: WithoutMarkers));
        Assert.AreEqual("alex@contoso.com|", box.ToString());
        Assert.AreEqual(2, markers.Count);
        Assert.IsTrue(box.Guard.Swallows(FakeTextBox.ToHookEvent(markers[0])));
        Assert.IsFalse(box.Guard.Swallows(FakeTextBox.UserKey('z', up: false)), "One turning up late after all changes nothing.");
    }

    [TestMethod]
    public void TypedCharactersGoOutOneACallPaced()
    {
        // Windows 11 Notepad smeared characters sent together, and even one a call without a pause: "Status: eeee".
        using var box = new FakeTextBox("Status: !d");
        var sleeps = new List<int>();
        Assert.IsTrue(Run(box, 2, "Hi. Ok\n", cursorLeft: 1, sleep: sleeps.Add));
        Assert.AreEqual("Mark | BS BS | H | i | . |   | O | k | Enter | Left | Mark", Calls(box));
        Assert.AreEqual("15 25 25 50 25 25 25 50", string.Join(" ", sleeps), "The pause before the erase, then one after each character, longer after a sentence.");
        Assert.AreEqual("Status: Hi. Ok|\n", box.ToString());
    }

    [TestMethod]
    public void TheTargetIsCheckedBeforeEveryCharacterAndLosingItIsFinal()
    {
        // Checked every eight UTF-16 units, a run of emoji could skip every check.
        using var box = new FakeTextBox("!d");
        IntPtr foreground = Target;
        box.BeforeCall = calls =>
        {
            if (calls == 3)
            {
                foreground = new IntPtr(11);
            }
        };

        Assert.IsFalse(Run(box, 2, "😀😀😀", foreground: () => foreground, target: Target));
        Assert.AreEqual("😀😀|", box.ToString());
        Assert.AreEqual("Mark", FakeTextBox.Describe(box.Batches[^1]));
        Assert.IsTrue(box.Guard.Cancelled);
    }

    [TestMethod]
    public void AfterTheHoldCapTheHeldKeysGoBackAndTheRestIsTypedUnguarded()
    {
        // A refused paste of a long snippet falls back to typing, which held the keyboard for over 25 s.
        long now = 0;
        using var box = new FakeTextBox(string.Empty);
        box.Guard.Clock = () => now;
        box.BeforeCall = calls =>
        {
            if (calls == 10)
            {
                box.UserTypes("u");
            }
            else if (calls == 90)
            {
                box.UserTypes("v");
            }
        };

        Assert.IsTrue(Run(box, 0, new string('x', 100), sleep: ms => now += ms));
        Assert.AreEqual(new string('x', 80) + "u" + new string('x', 7) + "v" + new string('x', 13) + "|", box.ToString());
        Assert.AreEqual("Mark", FakeTextBox.Describe(box.Batches[81]), "Ended after 2 s of typing.");
        Assert.AreEqual("u Mark", FakeTextBox.Describe(box.Batches[82]));
    }

    [TestMethod]
    public void ALongEraseGoesOutInPartsWithTheTargetCheckedBeforeEach()
    {
        using var box = new FakeTextBox(new string('x', 600));
        int checks = 0;
        Assert.IsTrue(Run(box, 600, string.Empty, foreground: () =>
        {
            checks++;
            return new IntPtr(1);
        }));
        Assert.AreEqual("1 1000 200 1", string.Join(" ", box.Batches.Select(batch => batch.Length)));
        Assert.AreEqual("|", box.ToString());
        Assert.IsTrue(checks >= 4, "Before the work, before the start marker, and before each part.");
    }

    /// <summary>A guard whose sends come back through it, as through the hook: SendInput returns once every hook has seen them.</summary>
    private static InsertionGuard Guard(Func<long> generation, List<Native.INPUT[]> sent, Func<IntPtr, bool>? inFront = null)
    {
        var guard = new InsertionGuard(generation, inFront ?? (_ => true));
        guard.Resend = inputs =>
        {
            sent.Add([.. inputs]);
            foreach (Native.INPUT input in inputs)
            {
                _ = guard.Swallows(FakeTextBox.ToHookEvent(input));
            }

            return (uint)inputs.Length;
        };
        return guard;
    }

    private static Native.KBDLLHOOKSTRUCT OurKey(int vk, bool up) => FakeTextBox.UserVk(vk, up) with { DwExtraInfo = KeyboardHook.InjectionSignature };

    private static string Calls(FakeTextBox box) => string.Join(" | ", box.Batches.Select(FakeTextBox.Describe));

    private static bool Run(FakeTextBox box, int erase, string text, int cursorLeft = 0, InjectionBackend backend = InjectionBackend.Typing, Func<int, short>? keys = null, Action? inserted = null, Action<int>? sleep = null, Func<IntPtr>? foreground = null, IntPtr target = default, Func<Native.INPUT[], uint>? dispatch = null, Func<string, IntPtr, Func<(bool Whole, bool SentAny)>, PasteOutcome>? paste = null)
    {
        InjectionBackend original = Injector.Backend;
        try
        {
            Injector.Backend = backend;
            return Injector.ReplaceCore(erase, text, target, cursorLeft, _ => true, box.Guard, box.Hook.InputGeneration, foreground ?? (() => new IntPtr(1)), paste ?? box.Paste, dispatch ?? box.Dispatch, getAsyncKeyState: keys ?? (_ => 0), sleep: sleep ?? (_ => { }), inserted: inserted);
        }
        finally
        {
            Injector.Backend = original;
        }
    }
}
