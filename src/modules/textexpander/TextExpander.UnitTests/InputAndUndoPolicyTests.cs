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
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.txt"));
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
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.txt"));
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
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.txt"));
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        string trigger = new('a', length);
        File.WriteAllText(path, trigger + "=long\n");
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
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.txt"));
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
        int erases = 0;
        try
        {
            Injector.Backend = InjectionBackend.Typing;
            int typingAuthorizationChecks = 0;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "typing route",
                new IntPtr(10),
                0,
                _ => ++typingAuthorizationChecks == 1,
                () => new IntPtr(10),
                erase: (_, _) =>
                {
                    erases++;
                    return false;
                }));
            Assert.AreEqual(2, typingAuthorizationChecks);
            Assert.AreEqual(0, erases);

            Injector.Backend = InjectionBackend.Clipboard;
            int clipboardAuthorizationChecks = 0;
            int pasteAttempts = 0;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "clipboard route",
                new IntPtr(10),
                0,
                _ => ++clipboardAuthorizationChecks == 1,
                () => new IntPtr(10),
                (_, _, _, _) =>
                {
                    pasteAttempts++;
                    return PasteOutcome.RefusedBeforeErase;
                },
                (_, _) =>
                {
                    erases++;
                    return false;
                }));
            Assert.AreEqual(1, pasteAttempts);
            Assert.AreEqual(2, clipboardAuthorizationChecks);
            Assert.AreEqual(0, erases);

            // Undo uses Replace as well, so it must pass the same authorization gate.
            Injector.Backend = InjectionBackend.Typing;
            Assert.IsFalse(Injector.ReplaceCore(5, "original replacement", IntPtr.Zero, 0, _ => false));

            // Empty replacements can still erase a trigger or move the cursor.
            Assert.IsFalse(Injector.ReplaceCore(3, string.Empty, IntPtr.Zero, 0, _ => false));
            Assert.IsFalse(Injector.ReplaceCore(0, string.Empty, IntPtr.Zero, 4, _ => false));
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
        var box = new FakeTextBox("PREFIX");

        TypeInto(hook, box, "!abc ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        Assert.AreEqual("PREFIX!abc|", box.ToString(), "The space that released the trigger is swallowed.");

        // What the worker does with it.
        (string text, int cursor) = Variables.ExpandWithPromptRequests(work.Replacement, _ => string.Empty);
        int cursorLeft = Injector.CountDeletableUnits(text[cursor..]);
        Assert.IsTrue(ReplaceIn(box, Injector.CountDeletableUnits(work.TypedPrefix), text, cursorLeft: cursorLeft));
        Assert.AreEqual("PREFIXAb|XYZ ", box.ToString());
        Assert.IsTrue(hook.RecordExpansion(work.TypedText!, text, IntPtr.Zero, work.InputGeneration, cursorLeft));

        Assert.IsFalse(hook.ProcessKey(Native.VK_LCONTROL, 0, ctrl: false, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
        Assert.AreEqual(4, undo.CursorLeft);

        Assert.IsTrue(ReplaceIn(box, Injector.CountDeletableUnits(undo.Replacement), undo.Trigger, cursorRight: undo.CursorLeft));
        Assert.AreEqual("PREFIX!abc |", box.ToString());
    }

    [TestMethod]
    public void AKeyPressAfterTheMatchCancelsTheQueuedExpansion()
    {
        // The worker erased wherever the caret was by then: "Before !abc " and a Left arrow
        // came out as "BeforeEXPANDED c".
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using SnippetStore store = LoadStore(scratch, "!abc=EXPANDED");
        using var hook = new KeyboardHook(store);
        var box = new FakeTextBox("Before ");

        TypeInto(hook, box, "!abc ");
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        bool InputUnchanged() => hook.InputGeneration == work.InputGeneration;
        Assert.IsTrue(InputUnchanged());

        // Focus changing on its own, as when it comes back after a prompt, moves no caret.
        hook.ForegroundChanged(IntPtr.Zero, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, 0, 0, 0, 0);
        Assert.IsTrue(InputUnchanged());

        Assert.IsFalse(hook.ProcessKey(Native.VK_LEFT, 0, ctrl: false, alt: false, shift: false, win: false));
        box.Left(1, IntPtr.Zero);
        Assert.IsFalse(InputUnchanged());

        Assert.IsFalse(ReplaceIn(box, Injector.CountDeletableUnits(work.TypedPrefix), "EXPANDED ", inputUnchanged: InputUnchanged));
        Assert.AreEqual("Before !ab|c", box.ToString(), "Typing: nothing erased or inserted.");

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
                () => new IntPtr(1),
                (_, _, _, _) =>
                {
                    pasted = true;
                    return PasteOutcome.Pasted;
                },
                box.Backspace,
                box.Insert,
                box.Left,
                inputUnchanged: InputUnchanged));
            Assert.IsFalse(pasted, "Pasting: the clipboard isn't even borrowed.");
        }
        finally
        {
            Injector.Backend = original;
        }

        Assert.AreEqual("Before !ab|c", box.ToString());
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
        var box = new FakeTextBox("Before ");

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

        box.Left(1, IntPtr.Zero);
        Assert.IsFalse(InputUnchanged());

        hook.Suspended = false;
        Assert.IsFalse(ReplaceIn(box, Injector.CountDeletableUnits(work.TypedPrefix), "Dear team ", inputUnchanged: InputUnchanged));
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
        // Borrowing the clipboard and the pause before backspacing both leave time for a key press.
        InjectionBackend originalBackend = Injector.Backend;
        int originalDelay = Injector.PreBackspaceDelayMs;
        try
        {
            Injector.Backend = InjectionBackend.Typing;
            Injector.PreBackspaceDelayMs = 0;

            // Never the real foreground window, so no input can reach the desktop even if the check were gone.
            var target = new IntPtr(-1);
            int checks = 0;
            bool typed = false;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                target,
                0,
                _ => true,
                () => target,
                sendText: (_, _) =>
                {
                    typed = true;
                    return true;
                },
                sendLeft: (_, _) => true,
                inputUnchanged: () => ++checks == 1));
            Assert.AreEqual(2, checks, "Asked when the work starts and again by the erase itself.");
            Assert.IsFalse(typed);
        }
        finally
        {
            Injector.Backend = originalBackend;
            Injector.PreBackspaceDelayMs = originalDelay;
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
                box.Insert(ch.ToString(), IntPtr.Zero);
            }
        }
    }

    private static SnippetStore LoadStore(ScratchDirectory scratch, string snippet, bool caseSensitive = true)
    {
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, snippet + "\n");
        var store = new SnippetStore(path, caseSensitive: caseSensitive);
        Assert.AreEqual(1, store.Load().Count);
        return store;
    }

    /// <summary>Runs the injector's typing path against <paramref name="box"/> instead of the real input queue.</summary>
    private static bool ReplaceIn(FakeTextBox box, int eraseCount, string text, int cursorLeft = 0, int cursorRight = 0, Func<bool>? inputUnchanged = null)
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
                () => new IntPtr(1),
                erase: box.Backspace,
                sendText: box.Insert,
                sendLeft: box.Left,
                cursorRightUnits: cursorRight,
                sendRight: box.Right,
                inputUnchanged: inputUnchanged);
        }
        finally
        {
            Injector.Backend = original;
        }
    }

    /// <summary>A text box for the injector's fakes: its text, and the caret as an index into it.</summary>
    private sealed class FakeTextBox
    {
        public FakeTextBox(string text)
        {
            Text = text;
            Caret = text.Length;
        }

        public string Text { get; private set; }

        public int Caret { get; private set; }

        public bool Insert(string value, IntPtr target)
        {
            Text = Text.Insert(Caret, value);
            Caret += value.Length;
            return true;
        }

        public bool Backspace(int count, IntPtr target)
        {
            Text = Text.Remove(Caret - count, count);
            Caret -= count;
            return true;
        }

        public bool Left(int count, IntPtr target)
        {
            Caret -= count;
            return true;
        }

        public bool Right(int count, IntPtr target)
        {
            Caret += count;
            return true;
        }

        /// <summary>Shows the caret as "|".</summary>
        public override string ToString() => Text.Insert(Caret, "|");
    }
}
