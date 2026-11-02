// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InputAndUndoPolicyTests
{
    [TestMethod]
    public void PendingExpansionCarriesTypedTextForOneStepUndo()
    {
        var expansion = new PendingExpansion("btw", "btw", "by the way ", IntPtr.Zero, TypedText: "btw ");
        Assert.AreEqual("btw ", expansion.TypedText);
        Assert.IsFalse(expansion.IsUndo);

        var undo = new PendingExpansion("btw ", string.Empty, "by the way ", IntPtr.Zero, IsUndo: true);
        Assert.IsTrue(undo.IsUndo);
    }

    [TestMethod]
    public void IntegrityCeilingNeverExceedsMedium()
    {
        Assert.AreEqual(InputTarget.MediumIntegrity, InputTarget.TrustCeiling);
    }

    [TestMethod]
    public void CtrlKeyDownBeforeZKeepsTheExpansionUndoable()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.json"));
        using var hook = new KeyboardHook(store);
        hook.RecordExpansion("!mysig", "Best regards", IntPtr.Zero);

        // The low-level hook sees each modifier as its own key-down before Z, first without and
        // then (on auto-repeat) with the modifier reported as held.
        Assert.IsFalse(hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));

        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
        Assert.IsTrue(undo.IsUndo);
        Assert.AreEqual("!mysig", undo.Trigger);
        Assert.AreEqual("Best regards", undo.Replacement);

        // Undo is one-step: a second Ctrl+Z belongs to the application.
        Assert.IsFalse(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.AreEqual(0, hook.Pending.Count);
    }

    [TestMethod]
    public void ShiftKeyDownDoesNotDiscardUndoButOtherKeysDo()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.json"));
        using var hook = new KeyboardHook(store);

        hook.RecordExpansion("!mysig", "Best regards", IntPtr.Zero);
        Assert.IsFalse(hook.ProcessKey(Native.VK_RSHIFT, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(hook.ProcessKey(Native.VK_RCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.Pending.TryTake(out _));

        hook.RecordExpansion("!mysig", "Best regards", IntPtr.Zero);
        Assert.IsFalse(hook.ProcessKey(Native.VK_RETURN, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.AreEqual(0, hook.Pending.Count);
    }

    [TestMethod]
    public void LateExpansionCannotRestoreUndoAfterNewUserInput()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.json"));
        using var hook = new KeyboardHook(store);

        long staleGeneration = hook.InputGeneration;
        Assert.IsFalse(hook.ProcessKey(Native.VK_PACKET, 'x', ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(hook.RecordExpansion("!old", "old replacement", IntPtr.Zero, staleGeneration));
        Assert.IsFalse(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.AreEqual(0, hook.Pending.Count);
    }

    [TestMethod]
    public void AfterAPromptTypingWaitsOnlyUntilTheWindowIsInFront()
    {
        // PERF-9: every prompted snippet used to wait a fixed 350 ms before typing.
        var target = new IntPtr(42);
        var sleeps = new List<int>();
        long now = 0;
        void Sleep(int ms)
        {
            sleeps.Add(ms);
            now += ms;
        }

        Assert.IsTrue(WindowFocus.WaitUntilInFront(target, 350, () => target, Sleep, () => now));
        Assert.AreEqual(WindowFocus.SettleMs, sleeps.Single(), "Already in front: only the settle.");

        sleeps.Clear();
        now = 0;
        int checks = 0;
        Assert.IsTrue(WindowFocus.WaitUntilInFront(target, 350, () => ++checks > 3 ? target : IntPtr.Zero, Sleep, () => now));
        Assert.AreEqual((3 * 5) + WindowFocus.SettleMs, now, "Three short checks, then the settle.");

        now = 0;
        Assert.IsFalse(WindowFocus.WaitUntilInFront(target, 350, () => IntPtr.Zero, Sleep, () => now));
        Assert.AreEqual(350, now, "Gives up at the cap; the caller then doesn't type.");
    }

    // The brute-force cases (FUZZ-3): from an empty buffer, 257 in the default mode and 256 in
    // word mode used to wrap on the last keystroke. After other typing, anything past 126 could.
    [TestMethod]
    [DataRow(false, 257, 0)]
    [DataRow(true, 256, 0)]
    [DataRow(false, 127, 200)]
    [DataRow(true, 127, 200)]
    [DataRow(false, 600, 1000)]
    [DataRow(true, 600, 1000)]
    public void ATriggerLongerThanHalfTheDefaultBufferStillFires(bool wordMode, int length, int typedBefore)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        string trigger = new('a', length);
        File.WriteAllText(path, SnippetJson.Of(trigger + "=long"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store);

        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = wordMode;

            // Whole words ending in a space, so the trigger still starts a word in word mode.
            string before = string.Concat(Enumerable.Repeat("word ", (typedBefore + 4) / 5));
            Assert.IsFalse(Type(hook, before));
            Assert.AreEqual(!wordMode, Type(hook, trigger), "The default mode fires on the trigger's last character.");
            if (wordMode)
            {
                Assert.IsTrue(Type(hook, " "), "Word mode fires on the space after it.");
            }

            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual(trigger, expansion.Trigger);
            Assert.AreEqual(0, hook.Pending.Count);
        }
        finally
        {
            WordBoundary.Required = original;
        }
    }

    [TestMethod]
    public void TheBufferHoldsTheLongestTriggerWithRoomOnBothSides()
    {
        Assert.AreEqual(256, KeyboardHook.CapacityFor(0));
        Assert.AreEqual(256, KeyboardHook.CapacityFor(126));
        Assert.AreEqual(258, KeyboardHook.CapacityFor(127));

        // A full buffer keeps its newest half: the trigger, the character in front of it and the
        // space after it must all survive that.
        for (int length = 1; length < 2000; length += 37)
        {
            Assert.IsTrue(KeyboardHook.CapacityFor(length) / 2 >= length + 2, $"length {length}");
        }
    }

    [TestMethod]
    public void RedoAndAltGrChordsDoNotReplayTheTrigger()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.json"));
        using var hook = new KeyboardHook(store);
        hook.RecordExpansion("!mysig", "Best regards", IntPtr.Zero);

        Assert.IsFalse(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: true, win: false));
        Assert.IsFalse(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: true, shift: false, win: false));
        Assert.AreEqual(0, hook.Pending.Count);

        Assert.IsTrue(KeyboardHook.IsUndoChord(Native.VK_Z, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsFalse(KeyboardHook.IsUndoChord(Native.VK_Z, ctrl: true, alt: false, shift: false, win: true));
        foreach (int modifier in new[] { Native.VK_SHIFT, Native.VK_CONTROL, Native.VK_MENU, Native.VK_LSHIFT, Native.VK_RSHIFT, Native.VK_LCONTROL, Native.VK_RCONTROL, Native.VK_LMENU, Native.VK_RMENU, Native.VK_LWIN, Native.VK_RWIN })
        {
            Assert.IsTrue(KeyboardHook.IsModifierKey((uint)modifier), $"0x{modifier:X2}");
        }

        Assert.IsFalse(KeyboardHook.IsModifierKey(Native.VK_CAPITAL));
        Assert.IsFalse(KeyboardHook.IsModifierKey(Native.VK_Z));
    }

    [TestMethod]
    public void UndoWaitsForHeldModifiersAndAbandonsWhenTheyStayDown()
    {
        Assert.IsTrue(Injector.WaitForModifierRelease(50, _ => false));

        int ctrlChecks = 0;
        Assert.IsTrue(Injector.WaitForModifierRelease(Injector.UndoModifierReleaseTimeoutMs, vk => vk == Native.VK_CONTROL && ++ctrlChecks <= 3));
        Assert.AreEqual(4, ctrlChecks);

        Assert.IsFalse(Injector.WaitForModifierRelease(30, vk => vk == Native.VK_CONTROL));
    }

    [TestMethod]
    public void HookOnlyCapturesKeysForCurrentAuthorizedWindowAndProcess()
    {
        var authorized = new KeyboardHook.TargetAuthorization(new IntPtr(10), 20, CanDrive: true);

        Assert.IsTrue(KeyboardHook.IsAuthorizedForeground(new IntPtr(10), 20, authorized));
        Assert.IsFalse(KeyboardHook.IsAuthorizedForeground(new IntPtr(11), 20, authorized));
        Assert.IsFalse(KeyboardHook.IsAuthorizedForeground(new IntPtr(10), 21, authorized));
        Assert.IsFalse(KeyboardHook.IsAuthorizedForeground(new IntPtr(10), 20, authorized with { CanDrive = false }));
        Assert.IsFalse(KeyboardHook.IsAuthorizedForeground(IntPtr.Zero, 0, authorized));
    }

    [TestMethod]
    public void AWindowInFrontThatWasNeverCheckedIsCheckedOnce()
    {
        // The foreground event checked the window being left (10): AttachThreadInput activation,
        // as when a prompt hands focus back, reports the change before GetForegroundWindow does.
        var stale = new KeyboardHook.TargetAuthorization(new IntPtr(10), 20, CanDrive: true);

        Assert.IsTrue(KeyboardHook.NeedsAuthorization(new IntPtr(11), stale, IntPtr.Zero), "The window actually in front gets checked.");
        Assert.IsFalse(KeyboardHook.NeedsAuthorization(new IntPtr(11), stale, new IntPtr(11)), "Once, not on every key while the check runs.");
        Assert.IsTrue(KeyboardHook.NeedsAuthorization(new IntPtr(11), stale, new IntPtr(12)), "An older request doesn't hold it up.");
        Assert.IsFalse(KeyboardHook.NeedsAuthorization(new IntPtr(10), stale with { CanDrive = false }, IntPtr.Zero), "A refused window isn't asked about again.");
        Assert.IsFalse(KeyboardHook.NeedsAuthorization(IntPtr.Zero, stale, IntPtr.Zero));
    }

    [TestMethod]
    public void ClipboardSnapshotZeroesCopiedBytesWhenDisposed()
    {
        byte[] data = [1, 2, 3, 4];
        using (var snapshot = new ClipboardSnapshot([new ClipboardEntry(ClipboardFormats.CF_UNICODETEXT, data)]))
        {
            Assert.IsFalse(snapshot.IsEmpty);
        }

        CollectionAssert.AreEqual(new byte[data.Length], data);
    }

    [TestMethod]
    public void RuntimeInputSnippetsAreNotEligibleForUndoRetention()
    {
        Assert.IsTrue(Variables.UsesSensitiveRuntimeInput("Email: {{prompt:address}}"));
        Assert.IsTrue(Variables.UsesSensitiveRuntimeInput("Copied: {{clipboard}}"));
        Assert.IsFalse(Variables.UsesSensitiveRuntimeInput("Date: {{isodate}}"));
    }

    [TestMethod]
    public void UnauthorizedTargetIsRejectedBeforeTypingClipboardOrUndoInjection()
    {
        InjectionBackend originalBackend = Injector.Backend;
        int sends = 0;
        uint Send(Native.INPUT[] inputs)
        {
            sends++;
            return (uint)inputs.Length;
        }

        try
        {
            using var guard = new InsertionGuard(() => 0, _ => true);
            Injector.Backend = InjectionBackend.Typing;
            int typingAuthorizationChecks = 0;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "typing route",
                new IntPtr(10),
                0,
                _ => ++typingAuthorizationChecks == 1,
                guard,
                0,
                () => new IntPtr(10),
                dispatch: Send,
                sleep: _ => { }));
            Assert.AreEqual(2, typingAuthorizationChecks);
            Assert.AreEqual(0, sends);

            Injector.Backend = InjectionBackend.Clipboard;
            int clipboardAuthorizationChecks = 0;
            int pasteAttempts = 0;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "clipboard route",
                new IntPtr(10),
                0,
                _ => ++clipboardAuthorizationChecks == 1,
                guard,
                0,
                () => new IntPtr(10),
                (_, _, _, _) =>
                {
                    pasteAttempts++;
                    return PasteOutcome.RefusedBeforeErase;
                },
                Send,
                sleep: _ => { }));
            Assert.AreEqual(0, pasteAttempts, "Refused before the clipboard is borrowed.");
            Assert.AreEqual(2, clipboardAuthorizationChecks);
            Assert.AreEqual(0, sends);

            // Undo uses Replace as well, so it must pass the same authorization gate.
            Injector.Backend = InjectionBackend.Typing;
            Assert.IsFalse(Injector.ReplaceCore(5, "original replacement", IntPtr.Zero, 0, _ => false, guard, 0));

            // Empty replacements can still erase a trigger or move the cursor.
            Assert.IsFalse(Injector.ReplaceCore(3, string.Empty, IntPtr.Zero, 0, _ => false, guard, 0));
            Assert.IsFalse(Injector.ReplaceCore(0, string.Empty, IntPtr.Zero, 4, _ => false, guard, 0));
        }
        finally
        {
            Injector.Backend = originalBackend;
        }
    }

    [TestMethod]
    public void PasswordEditDetectionRequiresPasswordStyleAndKnownEditClass()
    {
        Assert.IsTrue(KeyboardHook.IsPasswordEditControl("Edit", Native.ES_PASSWORD));
        Assert.IsTrue(KeyboardHook.IsPasswordEditControl("RichEdit20W", Native.ES_PASSWORD));
        Assert.IsFalse(KeyboardHook.IsPasswordEditControl("Edit", 0));
        Assert.IsFalse(KeyboardHook.IsPasswordEditControl("Button", Native.ES_PASSWORD));

        // WinForms password boxes, by class name and style only.
        Assert.IsTrue(KeyboardHook.IsPasswordEditControl("WindowsForms10.EDIT.app.0.141b42a_r6_ad1", Native.ES_PASSWORD));
        Assert.IsTrue(KeyboardHook.IsPasswordEditControl("windowsforms10.edit.app.0.2bf8098_r6_ad1", Native.ES_PASSWORD | 0x0080));
        Assert.IsFalse(KeyboardHook.IsPasswordEditControl("WindowsForms10.EDIT.app.0.141b42a_r6_ad1", 0), "An ordinary WinForms text box.");
        Assert.IsFalse(KeyboardHook.IsPasswordEditControl("WindowsForms10.BUTTON.app.0.141b42a_r6_ad1", Native.ES_PASSWORD));
        Assert.IsFalse(KeyboardHook.IsPasswordEditControl("WindowsForms10.EDITOR", Native.ES_PASSWORD));

        // Not detectable this way, which is why the notice says snippets can still expand there.
        Assert.IsFalse(KeyboardHook.IsPasswordEditControl("Chrome_RenderWidgetHostHWND", Native.ES_PASSWORD));
    }

    [TestMethod]
    public void CursorMarkerLeavesCleanTextAndCountsSurrogatePairAsTwoDeletes()
    {
        string text = "abc$|$de😀";
        int marker = text.IndexOf("$|$", StringComparison.Ordinal);
        Assert.AreEqual(3, Injector.CountDeletableUnits(text[(marker + 3)..]));
        Assert.AreEqual("abcde😀", text.Remove(marker, 3));
    }

    [TestMethod]
    public void PowerToysSettingsIsNeverATargetButOtherProcessesAre()
    {
        // Expanding inside Settings would rewrite the trigger being typed into the snippet editor.
        Assert.IsTrue(InputTarget.IsPowerToysSettingsImage(@"C:\Program Files\PowerToys\WinUI3Apps\PowerToys.Settings.exe"));
        Assert.IsTrue(InputTarget.IsPowerToysSettingsImage(@"c:\pt\arm64\debug\winui3apps\powertoys.settings.EXE"));
        Assert.IsFalse(InputTarget.IsPowerToysSettingsImage(@"C:\Program Files\PowerToys\PowerToys.exe"));
        Assert.IsFalse(InputTarget.IsPowerToysSettingsImage(@"C:\Windows\System32\notepad.exe"));
        Assert.IsFalse(InputTarget.IsPowerToysSettingsImage(@"C:\Temp\PowerToys.Settings.exe.bak"));
        Assert.IsTrue(InputTarget.IsPowerToysSettingsImage(@"C:\Program Files\PowerToys\WinUI3Apps\PowerToys.TextExpander.Editor.exe"));
        Assert.IsTrue(InputTarget.IsPowerToysSettingsImage(@"C:\pt\POWERTOYS.TEXTEXPANDER.EDITOR.EXE"));
        Assert.IsFalse(InputTarget.IsPowerToysSettingsImage(@"C:\Temp\PowerToys.TextExpander.Editor.exe.bak"));
        Assert.IsFalse(InputTarget.IsPowerToysSettingsImage(null));

        // Reads a real process's image path, the way the foreground check does.
        Assert.IsFalse(InputTarget.IsPowerToysSettings((uint)Environment.ProcessId));
    }

    [TestMethod]
    public void UndoOfACursorSnippetErasesFromTheEndOfTheExpansion()
    {
        // The $|$ leaves the caret inside the expansion. Backspacing from there turned "PREFIX" and
        // "!abc " back into "PR!abc XYZ " instead of "PREFIX!abc ".
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=Ab$|$XYZ");
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("PREFIX", hook);

        TypeInto(hook, box, "!abc ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        Assert.AreEqual("PREFIX!abc|", box.ToString(), "The space that released the trigger is swallowed.");

        // What the worker does with it.
        (string text, int cursor) = Variables.ExpandWithPromptRequests(work.Replacement, _ => string.Empty);
        Assert.AreEqual(' ', work.Terminator, "Added by the worker, so the hook doesn't copy the replacement.");
        text += work.Terminator;
        int cursorLeft = Injector.CountDeletableUnits(text[cursor..]);
        Assert.IsTrue(ReplaceIn(box, Injector.CountDeletableUnits(work.TypedPrefix), text, cursorLeft: cursorLeft, generation: work.InputGeneration));
        Assert.AreEqual("PREFIXAb|XYZ ", box.ToString());
        Assert.IsTrue(hook.RecordExpansion(work.TypedText!, text, IntPtr.Zero, work.InputGeneration, cursorLeft));

        Assert.IsFalse(hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
        Assert.AreEqual(4, undo.CursorLeft);

        int sent = box.Batches.Count;
        Assert.IsTrue(ReplaceIn(box, Injector.CountDeletableUnits(undo.Replacement), undo.Trigger, cursorRight: undo.CursorLeft, generation: undo.InputGeneration));
        Assert.AreEqual("PREFIX!abc |", box.ToString());
        Assert.AreEqual("Right Right Right Right BS BS BS BS BS BS", FakeTextBox.Describe(box.Batches[sent + 1]), "Back to the end of the expansion, then erased.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AKeyOrClickAfterTheMatchDropsTheExpansionAndLeavesItsSpaceOut(bool click)
    {
        // The worker erased wherever the caret was by then: "Before !abc " and a Left arrow, or a click, that put the
        // caret before the "c" came out as "BeforeEXPANDED c". Dropped, it leaves the space that completed the trigger
        // out too, as the hook swallowed it.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=EXPANDED");
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("Before ", hook);

        TypeInto(hook, box, "!abc ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        bool InputUnchanged() => hook.InputGeneration == work.InputGeneration;

        // Focus changing on its own, as when it comes back after a prompt, moves no caret.
        hook.ForegroundChanged(IntPtr.Zero, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, 0, 0, 0, 0);
        Assert.IsTrue(InputUnchanged());

        if (click)
        {
            hook.Click();
        }
        else
        {
            Assert.IsFalse(hook.ProcessKey(Native.VK_LEFT, 0, ctrl: false, alt: false, shift: false, win: false));
        }

        box.Caret--;
        Assert.IsFalse(InputUnchanged());

        // Nothing is erased, inserted or typed back.
        Assert.IsFalse(ReplaceIn(box, Injector.CountDeletableUnits(work.TypedPrefix), "EXPANDED ", generation: work.InputGeneration));
        Assert.AreEqual("Before !ab|c", box.ToString());

        // Pasting doesn't even borrow the clipboard.
        InjectionBackend original = Injector.Backend;
        try
        {
            Injector.Backend = InjectionBackend.Clipboard;
            bool pasted = false;
            Assert.IsFalse(Injector.ReplaceCore(
                Injector.CountDeletableUnits(work.TypedPrefix),
                "EXPANDED ",
                IntPtr.Zero,
                0,
                _ => true,
                hook.Guard,
                work.InputGeneration,
                () => new IntPtr(1),
                (_, _, _, _) =>
                {
                    pasted = true;
                    return PasteOutcome.Pasted;
                },
                box.Dispatch));
            Assert.IsFalse(pasted);
        }
        finally
        {
            Injector.Backend = original;
        }

        Assert.AreEqual(0, box.Batches.Count, "Nothing sent at all.");
    }

    [TestMethod]
    public void ThePasteGoesOutBetweenTheMarkers()
    {
        // The paste pressed Ctrl, V and let go of Ctrl in separate calls with pauses between them; a key typed in
        // between became Ctrl+A or Ctrl+S, and the paste replaced the whole document or opened Save As. An earlier $|$
        // left the caret before the "c", so it goes back to the end first.
        using var box = new FakeTextBox("Before !abc") { Caret = 10, Clipboard = "the user's" };
        InjectionBackend original = Injector.Backend;
        try
        {
            Injector.Backend = InjectionBackend.Clipboard;
            Assert.IsTrue(Injector.ReplaceCore(
                4,
                "Ab XYZ ",
                IntPtr.Zero,
                4,
                _ => true,
                box.Guard,
                box.Hook.InputGeneration,
                () => new IntPtr(1),
                box.Paste,
                box.Dispatch,
                cursorRightUnits: 1,
                getAsyncKeyState: _ => 0,
                sleep: _ => { }));
        }
        finally
        {
            Injector.Backend = original;
        }

        Assert.AreEqual("Mark | Right BS BS BS BS | Ctrl-down V Ctrl-up | Left Left Left Left | Mark", string.Join(" | ", box.Batches.Select(FakeTextBox.Describe)));
        Assert.AreEqual("Before Ab |XYZ ", box.ToString());
        Assert.AreEqual("the user's", box.Clipboard);
    }

    [TestMethod]
    public void TheWorkerHandsTheInjectorTheGuardAndTheGenerationOfTheMatch()
    {
        // The guard decides when the insertion starts whether a key or click since the match drops it. A key typed
        // before the worker gets to it drops it outright, and nothing is typed back.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=EXPANDED");
        using var hook = new KeyboardHook(store);
        var calls = new List<string>();
        Program.ReplaceText replace = (backspaces, text, _, _, right, guard, generation, inserted, givenBack) =>
        {
            calls.Add($"{backspaces} \"{text}\" {right} {guard == hook.Guard} {generation} {inserted is not null} {givenBack}");
            inserted?.Invoke();
            return true;
        };

        Assert.IsTrue(Type(hook, "!abc "));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        Assert.IsTrue(Program.TryExpand(hook, work with { TargetWindow = IntPtr.Zero }, replace));

        Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
        Assert.IsTrue(Program.TryExpand(hook, undo, replace, isKeyDown: _ => false));

        Assert.IsTrue(Type(hook, "!abc "));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion late));
        Assert.IsFalse(hook.ProcessKey(Native.VK_LEFT, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(Program.TryExpand(hook, late with { TargetWindow = IntPtr.Zero }, replace));

        Assert.AreEqual($"4 \"EXPANDED \" 0 True {work.InputGeneration} True 1, 9 \"!abc \" 0 True {undo.InputGeneration} False 0", string.Join(", ", calls), "Only the space added after the expansion is given back.");
    }

    [TestMethod]
    public void ACaretFarFromTheEndStaysAtTheEnd()
    {
        // A $|$ at the start of a huge snippet took millions of Left presses.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("!far=$|$" + new string('x', Injector.MaxCaretMoves)));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store);
        var calls = new List<string>();
        Program.ReplaceText replace = (_, _, _, left, _, _, _, inserted, _) =>
        {
            calls.Add($"{left} {inserted is not null}");
            return true;
        };

        Assert.IsTrue(Type(hook, "!far "));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        Assert.IsTrue(Program.TryExpand(hook, work with { TargetWindow = IntPtr.Zero }, replace));
        Assert.AreEqual("0 True", calls.Single());
    }

    [TestMethod]
    [DataRow("abcd", false)]
    [DataRow("abcde", true)]
    public void TheSpaceGivenBackDoesntCountTowardTheMinimumLengthToPaste(string replacement, bool pasted)
    {
        // With Automatic and the minimum of 5, a 4-character snippet and the space after it came to 5 and were pasted,
        // though Settings says one-line snippets shorter than that are typed.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=" + replacement);
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("Before ", hook);
        TypeInto(hook, box, "!abc ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));

        InjectionBackend backend = Injector.Backend;
        int threshold = Injector.ClipboardThresholdChars;
        bool usedClipboard = false;
        try
        {
            Injector.Backend = InjectionBackend.Auto;
            Injector.ClipboardThresholdChars = 5;
            Assert.IsTrue(Program.TryExpand(hook, work with { TargetWindow = IntPtr.Zero }, Into(box, (text, window, send, keepAlive) =>
            {
                usedClipboard = true;
                return box.Paste(text, window, send, keepAlive);
            })));
        }
        finally
        {
            Injector.Backend = backend;
            Injector.ClipboardThresholdChars = threshold;
        }

        Assert.AreEqual(pasted, usedClipboard, replacement);
        Assert.AreEqual($"Before {replacement} |", box.ToString());
    }

    [TestMethod]
    public void OnlyAModifierPressedBeforeTheInsertionStartsLetsItGoAhead()
    {
        // Shift or Ctrl on their own, even remapped and so not matched, move no caret; any other key may.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=EXPANDED");
        using var hook = new KeyboardHook(store);
        Assert.IsTrue(Type(hook, "!abc "));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));

        bool GoesAhead()
        {
            Native.KBDLLHOOKSTRUCT marker = FakeTextBox.Marker(hook.Guard.Begin(work.InputGeneration, new IntPtr(1)));
            Assert.IsTrue(hook.Guard.Swallows(marker));
            bool committed = hook.Guard.AwaitStart();
            Assert.IsTrue(hook.Guard.Swallows(marker));
            hook.Guard.AwaitEnd(markerSent: true, []);
            return committed;
        }

        Assert.IsFalse(hook.ProcessKey(Native.VK_LSHIFT, 0, ctrl: false, alt: false, shift: false, win: false));
        hook.UnmatchedKey(Native.VK_RCONTROL);
        Assert.IsTrue(GoesAhead());

        Assert.IsFalse(hook.ProcessKey(Native.VK_LEFT, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsFalse(GoesAhead());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AKeyOrClickReachingTheTargetWhileAPromptIsOpenCancelsTheQueuedExpansion(bool click)
    {
        // The prompt suspends the hook, which then let every key and click pass unseen: a key typed into the
        // target before the prompt took focus, or a click there while it was open, moved the caret, and after
        // OK the Backspaces deleted other text.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=Dear {{prompt:Name}}");
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("Before ", hook);

        TypeInto(hook, box, "!abc ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        bool InputUnchanged() => hook.InputGeneration == work.InputGeneration;

        hook.SuspendFor(new IntPtr(10));

        // Answering the prompt changes nothing: typing and clicking in it or in another app (to copy the answer
        // from a browser, say), a modifier pressed in the target, and focus going to the prompt and back.
        hook.KeyWhileSuspended(Native.VK_PACKET, reachesTarget: false);
        hook.KeyWhileSuspended(Native.VK_RETURN, reachesTarget: false);
        hook.ClickWhileSuspended(reachesTarget: false);
        hook.KeyWhileSuspended(Native.VK_LCONTROL, reachesTarget: true);
        hook.ForegroundChanged(IntPtr.Zero, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, 0, 0, 0, 0);
        Assert.IsTrue(InputUnchanged());

        if (click)
        {
            hook.ClickWhileSuspended(reachesTarget: true);
        }
        else
        {
            hook.KeyWhileSuspended(Native.VK_LEFT, reachesTarget: true);
        }

        box.Caret--;
        Assert.IsFalse(InputUnchanged());

        hook.Suspended = false;
        Assert.IsFalse(ReplaceIn(box, Injector.CountDeletableUnits(work.TypedPrefix), "Dear team ", generation: work.InputGeneration));
        Assert.AreEqual("Before !ab|c", box.ToString(), "Nothing erased or inserted.");
    }

    [TestMethod]
    public void WhileAPromptIsOpenOnlyInputReachingTheTargetCounts()
    {
        // Every key and click outside the engine counted, so copying the answer from a browser, or picking it
        // from Win+V or the emoji panel, dropped the expansion.
        var target = new IntPtr(10);
        var prompt = new IntPtr(11);
        var browser = new IntPtr(12);
        bool IsOurs(IntPtr window) => window == prompt;

        Assert.IsTrue(KeyboardHook.ReachesTarget(target, target, IsOurs));
        Assert.IsFalse(KeyboardHook.ReachesTarget(browser, target, IsOurs));
        Assert.IsFalse(KeyboardHook.ReachesTarget(prompt, target, IsOurs));
        Assert.IsFalse(KeyboardHook.ReachesTarget(IntPtr.Zero, target, IsOurs));

        // With the target unknown, any window but ours might be it.
        Assert.IsTrue(KeyboardHook.ReachesTarget(browser, IntPtr.Zero, IsOurs));
        Assert.IsTrue(KeyboardHook.ReachesTarget(IntPtr.Zero, IntPtr.Zero, IsOurs), "No window counts as another app's.");
        Assert.IsFalse(KeyboardHook.ReachesTarget(prompt, IntPtr.Zero, IsOurs));
    }

    [TestMethod]
    [DataRow("{{prompt:First name}}", true, true, "!hello ")]
    [DataRow("{{prompt?:First name}}", true, true, "!hello ")]
    [DataRow("{{prompt:First name}}", true, false, "!hello ")]
    [DataRow("{{prompt?:First name}}", false, true, "!hello")]
    public void CancellingAQuestionCancelsTheWholeSnippet(string question, bool wordMode, bool keepTerminator, string typed)
    {
        // Esc, Cancel or the close button left that spot blank and asked the next question, so "Hi , thanks for reaching
        // out!" went in. Now nothing more is asked, nothing is erased or inserted, and the keystroke the hook swallowed,
        // the space or the trigger's last character, goes back. A key typed right after lands after it.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!hello=Hi " + question + ", thanks for reaching out! {{prompt:Team}}");
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("Before ", hook);
        Program.ReplaceText insert = Into(box);
        var asked = new List<string>();
        var calls = new List<string>();

        bool originalWordMode = WordBoundary.Required;
        bool originalKeep = WordBoundary.KeepTerminator;
        try
        {
            WordBoundary.Required = wordMode;
            WordBoundary.KeepTerminator = keepTerminator;
            TypeInto(hook, box, typed);
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
            Assert.AreEqual("Before " + typed[..^1] + "|", box.ToString(), "The hook swallowed the last keystroke.");

            Assert.IsFalse(Program.TryExpand(
                hook,
                work with { TargetWindow = IntPtr.Zero },
                (backspaces, text, target, left, right, guard, generation, inserted, givenBack) =>
                {
                    calls.Add($"{backspaces} \"{text}\" {inserted is null} {givenBack}");

                    // Typed right after Esc: held, so it lands after the keystroke given back.
                    box.UserTypes("x");
                    return insert(backspaces, text, target, left, right, guard, generation, inserted, givenBack);
                },
                isKeyDown: _ => false,
                ask: (request, _, _) =>
                {
                    asked.Add(request.Label);
                    return PromptText.Result(accepted: false, "typed, then cancelled");
                }));
        }
        finally
        {
            WordBoundary.Required = originalWordMode;
            WordBoundary.KeepTerminator = originalKeep;
        }

        Assert.AreEqual("First name", string.Join(" | ", asked), "Nothing more is asked.");
        Assert.AreEqual($"0 \"{typed[^1]}\" True 1", calls.Single(), "Nothing is erased, the swallowed keystroke goes back, and there's no undo.");
        Assert.AreEqual("Before " + typed + "x|", box.ToString(), "What was typed reads as typed, then the key typed after.");
    }

    [TestMethod]
    public void CancellingWhileAModifierIsStillDownGivesNothingBack()
    {
        // Alt still held a second after Alt+F4 closed the question: the keystroke given back would go out with Alt let go
        // and pressed again around it, so letting go of Alt would then open the app's menu. It's dropped instead.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!hello=Hi {{prompt:First name}}, thanks for reaching out!");
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("Before ", hook);
        TypeInto(hook, box, "!hello ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        int calls = 0;

        Assert.IsFalse(Program.TryExpand(
            hook,
            work with { TargetWindow = IntPtr.Zero },
            (_, _, _, _, _, _, _, _, _) => ++calls > 0,
            isKeyDown: vk => vk == Native.VK_MENU,
            ask: (_, _, _) => null));

        Assert.AreEqual(0, calls, "Nothing goes back while Alt is down.");
        Assert.AreEqual("Before !hello|", box.ToString());
    }

    [TestMethod]
    public void ARemapThatIsNotMatchedStillCountsAsInput()
    {
        // With remaps not counting as typing, they were dropped like the engine's own input: "!si", Caps Lock
        // remapped to Home, then "g" still completed "!sig", and its Backspaces erased text at the line start.
        const uint home = 0x24;
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!sig=Best regards");
        using var hook = new KeyboardHook(store);

        bool original = HostInputTags.AcceptRemappedInput;
        try
        {
            HostInputTags.AcceptRemappedInput = false;
            Assert.AreEqual(HostInputKind.Unmatched, HostInputTags.Classify(new IntPtr(0x11), KeyboardHook.InjectionSignature));

            Assert.IsFalse(Type(hook, "!si"));
            long before = hook.InputGeneration;
            hook.UnmatchedKey(home);
            Assert.AreNotEqual(before, hook.InputGeneration);
            _ = Type(hook, "g ");
            Assert.AreEqual(0, hook.Pending.Count, "\"!si\" is no longer in front of the caret.");

            // A remapped modifier moves no caret.
            Assert.IsFalse(Type(hook, "!si"));
            before = hook.InputGeneration;
            hook.UnmatchedKey(Native.VK_RSHIFT);
            Assert.AreEqual(before, hook.InputGeneration);
            _ = Type(hook, "g ");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual("!sig", expansion.Trigger);
        }
        finally
        {
            HostInputTags.AcceptRemappedInput = original;
        }
    }

    [TestMethod]
    public void TheCaretIsCheckedAgainRightBeforeTheFirstBackspace()
    {
        // Borrowing the clipboard and the pause before backspacing both leave time for a key press. The hook has the
        // last word when the insertion starts; asked first, the same question saves sending anything.
        long generation = 0;
        using var guard = new InsertionGuard(() => generation, _ => true);
        InjectionBackend originalBackend = Injector.Backend;
        try
        {
            Injector.Backend = InjectionBackend.Typing;
            int sends = 0;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                0,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                dispatch: inputs =>
                {
                    sends++;
                    return (uint)inputs.Length;
                },
                getAsyncKeyState: _ => 0,
                sleep: _ => generation++));
            Assert.AreEqual(1, generation, "A key pressed during the pause before backspacing.");
            Assert.AreEqual(0, sends);
        }
        finally
        {
            Injector.Backend = originalBackend;
        }
    }

    [TestMethod]
    [DataRow(Native.VK_LCONTROL, true, false, false, false, false)]
    [DataRow(Native.VK_LMENU, false, true, false, false, false)]
    [DataRow(Native.VK_RMENU, true, true, false, false, false)]
    [DataRow(Native.VK_LWIN, false, false, false, true, false)]
    [DataRow(Native.VK_LSHIFT, false, false, true, false, true)]
    [DataRow(0, false, false, false, false, true)]
    public void OnlyAPlainOrShiftedBackspaceIsTakenAsDeletingOneCharacter(int modifier, bool ctrl, bool alt, bool shift, bool win, bool stillExpands)
    {
        // Ctrl+Backspace deletes the whole word. The hook dropped one character, so "!abc",
        // Ctrl+Backspace and "c " still expanded a trigger no longer behind the caret.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=EXPANDED");
        using var hook = new KeyboardHook(store);

        Assert.IsFalse(Type(hook, "!abc"));
        if (modifier != 0)
        {
            Assert.IsFalse(hook.ProcessKey((uint)modifier, 0, ctrl: false, alt: false, shift: false, win: false));
        }

        Assert.IsFalse(hook.ProcessKey(Native.VK_BACK, 0, ctrl, alt, shift, win));
        Assert.AreEqual(stillExpands, Type(hook, "c "));
        Assert.AreEqual(stillExpands ? 1 : 0, hook.Pending.Count);
    }

    [TestMethod]
    [DataRow(true, "!ABC ", "!ABC")]
    [DataRow(false, "!ABC", "!AB")]
    public void UndoPutsBackTheCapitalizationThatWasTyped(bool wordMode, string typed, string erased)
    {
        // With matching that ignores case, Ctrl+Z after "!ABC " typed the saved "!abc " instead.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=EXPANDED", caseSensitive: false);
        using var hook = new KeyboardHook(store);

        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = wordMode;
            Assert.IsTrue(Type(hook, typed));
        }
        finally
        {
            WordBoundary.Required = original;
        }

        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        Assert.AreEqual("!abc", work.Trigger, "Matched against the saved trigger.");
        Assert.AreEqual(erased, work.TypedPrefix);
        Assert.AreEqual(typed, work.TypedText);

        // The worker keeps what was typed for undo, and Ctrl+Z types it back.
        Assert.IsTrue(hook.RecordExpansion(work.TypedText ?? work.Trigger, "EXPANDED", IntPtr.Zero, work.InputGeneration));
        Assert.IsFalse(hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
        Assert.AreEqual(typed, undo.Trigger);
    }

    /// <summary>Types text the way the touch keyboard does, so the layout doesn't matter.</summary>
    /// <returns>Whether the last character fired a trigger.</returns>
    private static bool Type(KeyboardHook hook, string text)
    {
        bool fired = false;
        foreach (char ch in text)
        {
            fired = hook.ProcessKey(Native.VK_PACKET, ch, ctrl: false, alt: false, shift: false, win: false);
        }

        return fired;
    }

    /// <summary>Types through the hook into <paramref name="box"/>, which never sees the keystrokes the hook swallows.</summary>
    private static void TypeInto(KeyboardHook hook, FakeTextBox box, string text)
    {
        foreach (char ch in text)
        {
            if (!hook.ProcessKey(Native.VK_PACKET, ch, ctrl: false, alt: false, shift: false, win: false))
            {
                box.Insert(ch.ToString());
            }
        }
    }

    private static SnippetStore LoadStore(ScratchDirectory scratch, string snippet, bool caseSensitive = true)
    {
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of(snippet));
        var store = new SnippetStore(path, caseSensitive: caseSensitive);
        Assert.AreEqual(1, store.Load().Count);
        return store;
    }

    /// <summary>Runs the injector's typing path against <paramref name="box"/> instead of the real input queue.</summary>
    private static bool ReplaceIn(FakeTextBox box, int eraseCount, string text, int cursorLeft = 0, int cursorRight = 0, long generation = 0)
    {
        InjectionBackend original = Injector.Backend;
        try
        {
            Injector.Backend = InjectionBackend.Typing;
            return Injector.ReplaceCore(
                eraseCount,
                text,
                IntPtr.Zero,
                cursorLeft,
                _ => true,
                box.Guard,
                generation,
                () => new IntPtr(1),
                dispatch: box.Dispatch,
                cursorRightUnits: cursorRight,
                getAsyncKeyState: _ => 0,
                sleep: _ => { });
        }
        finally
        {
            Injector.Backend = original;
        }
    }

    /// <summary>
    /// Runs <see cref="Program.TryExpand"/>'s insertions through the injector into <paramref name="box"/>, pasting with
    /// <paramref name="paste"/> or the box's own Ctrl+V.
    /// </summary>
    private static Program.ReplaceText Into(FakeTextBox box, Func<string, IntPtr, Func<(bool Whole, bool SentAny)>, Action, PasteOutcome>? paste = null)
        => (backspaces, text, target, left, right, guard, generation, inserted, givenBack) => Injector.ReplaceCore(
            backspaces,
            text,
            target,
            left,
            _ => true,
            guard,
            generation,
            () => new IntPtr(1),
            paste ?? box.Paste,
            box.Dispatch,
            cursorRightUnits: right,
            getAsyncKeyState: _ => 0,
            sleep: _ => { },
            inserted: inserted,
            givenBack: givenBack);
}
