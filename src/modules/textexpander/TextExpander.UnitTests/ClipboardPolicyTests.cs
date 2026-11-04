// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class ClipboardPolicyTests
{
    [TestMethod]
    public void CapturePlanPreservesRestorableFormatsAndRefusesHandles()
    {
        CollectionAssert.AreEqual(new uint[] { ClipboardFormats.CF_UNICODETEXT, ClipboardFormats.CF_TEXT, ClipboardFormats.CF_LOCALE }, ClipboardFormats.PlanCapture([ClipboardFormats.CF_UNICODETEXT, ClipboardFormats.CF_TEXT, ClipboardFormats.CF_LOCALE])!.ToArray());
        CollectionAssert.AreEqual(new uint[] { ClipboardFormats.CF_HDROP }, ClipboardFormats.PlanCapture([ClipboardFormats.CF_HDROP])!.ToArray());
        CollectionAssert.AreEqual(new uint[] { ClipboardFormats.CF_DIB, ClipboardFormats.CF_DIBV5 }, ClipboardFormats.PlanCapture([ClipboardFormats.CF_BITMAP, ClipboardFormats.CF_DIB, ClipboardFormats.CF_DIBV5])!.ToArray());
        Assert.IsNull(ClipboardFormats.PlanCapture([ClipboardFormats.CF_BITMAP]));
        Assert.IsNull(ClipboardFormats.PlanCapture([ClipboardFormats.CF_DIB, ClipboardFormats.CF_PALETTE]));
        Assert.IsNotNull(ClipboardFormats.PlanCapture(Array.Empty<uint>()));
    }

    [TestMethod]
    public void ClipboardLoanRefusesToOverwriteNewerClipboardContent()
    {
        var loan = new ClipboardLoan(sequenceAfterWrite: 41);
        Assert.IsTrue(loan.MayRestore(41));
        Assert.IsFalse(loan.MayRestore(42));
        loan.Rearm(43);
        Assert.IsTrue(loan.MayRestore(43));
    }

    [TestMethod]
    public void ClipboardLoanRearmKeepsRetriesFromLookingForeign()
    {
        var loan = new ClipboardLoan(sequenceAfterWrite: 41);
        Assert.IsFalse(loan.MayRestore(40));
        loan.Rearm(43);
        Assert.IsTrue(loan.MayRestore(43));
        Assert.IsFalse(loan.MayRestore(44));
    }

    [TestMethod]
    public void ACopyMadeWhileTheRestoreWaitsToOpenTheClipboardIsKept()
    {
        // The restore checked the clipboard was still ours, then waited to open it while another app held it, often to
        // write a new copy, then emptied the clipboard and put the old copy back over the new one. The check is asked
        // again once the clipboard is open.
        uint sequence = 41;
        int emptied = 0;
        ClipboardRestore CopiedDuringTheOpen(Func<uint, bool> stillOurs)
        {
            sequence++;
            if (!stillOurs(sequence))
            {
                return ClipboardRestore.Superseded;
            }

            emptied++;
            return ClipboardRestore.Restored;
        }

        Assert.AreEqual(ClipboardRestore.Superseded, Injector.HandBack(CopiedDuringTheOpen, new ClipboardLoan(41), () => sequence, _ => { }));
        Assert.AreEqual(0, emptied, "The new copy stays.");

        // Copied before the first attempt: nothing is opened at all.
        int attempts = 0;
        ClipboardRestore Counted(Func<uint, bool> stillOurs)
        {
            attempts++;
            return ClipboardRestore.Restored;
        }

        Assert.AreEqual(ClipboardRestore.Superseded, Injector.HandBack(Counted, new ClipboardLoan(41), () => 42, _ => { }));
        Assert.AreEqual(0, attempts);
    }

    [TestMethod]
    public void AnAttemptThatCouldntOpenTheClipboardDoesntAdoptACopyMadeMeanwhile()
    {
        // A failed attempt re-armed the loan with whatever the clipboard held next, so a copy made while the clipboard
        // couldn't be opened looked like ours, and the retry put the old copy back over it.
        uint sequence = 41;
        int calls = 0;
        ClipboardRestore HeldByAnAppThatCopied(Func<uint, bool> stillOurs)
        {
            if (++calls == 1)
            {
                sequence++;
                return ClipboardRestore.Untouched;
            }

            return stillOurs(sequence) ? ClipboardRestore.Restored : ClipboardRestore.Superseded;
        }

        Assert.AreEqual(ClipboardRestore.Superseded, Injector.HandBack(HeldByAnAppThatCopied, new ClipboardLoan(41), () => sequence, _ => { }));
        Assert.AreEqual(1, calls, "Not tried again over the new copy.");

        // Our own change still doesn't look like someone else's: an attempt that emptied the clipboard is retried.
        sequence = 41;
        calls = 0;
        ClipboardRestore PartlyThenWhole(Func<uint, bool> stillOurs)
        {
            if (!stillOurs(sequence))
            {
                return ClipboardRestore.Superseded;
            }

            if (++calls == 1)
            {
                sequence += 2;
                return ClipboardRestore.Incomplete;
            }

            return ClipboardRestore.Restored;
        }

        var sleeps = new List<int>();
        Assert.AreEqual(ClipboardRestore.Restored, Injector.HandBack(PartlyThenWhole, new ClipboardLoan(41), () => sequence, sleeps.Add));
        Assert.AreEqual(2, calls);
        CollectionAssert.AreEqual(new[] { Injector.RestoreRetryDelayMs }, sleeps);

        // Never able to open it: every attempt, then the failure, which the caller reports as a loss.
        calls = 0;
        ClipboardRestore NeverOpens(Func<uint, bool> stillOurs)
        {
            calls++;
            return ClipboardRestore.Untouched;
        }

        Assert.AreEqual(ClipboardRestore.Untouched, Injector.HandBack(NeverOpens, new ClipboardLoan(41), () => 41, _ => { }));
        Assert.AreEqual(Injector.RestoreAttempts, calls);
    }

    [TestMethod]
    public void TheSharedHandBackTriesAsOftenAsItsCallerSaysAndPausesOnlyBetweenTries()
    {
        // Text Expander and Command Palette's extension share Core's loop, each with its own tries and pause.
        int calls = 0;
        ClipboardRestore NeverOpens(Func<uint, bool> stillOurs)
        {
            calls++;
            return ClipboardRestore.Untouched;
        }

        var sleeps = new List<int>();
        Assert.AreEqual(ClipboardRestore.Untouched, ClipboardHandBack.Run(NeverOpens, new ClipboardLoan(41), () => 41, sleeps.Add, attempts: 4, retryDelayMs: 25));
        Assert.AreEqual(4, calls);
        Assert.AreEqual(3, sleeps.Count, "No pause after the last try.");
        Assert.IsTrue(sleeps.TrueForAll(ms => ms == 25));

        calls = 0;
        sleeps.Clear();
        Assert.AreEqual(ClipboardRestore.Untouched, ClipboardHandBack.Run(NeverOpens, new ClipboardLoan(41), () => 41, sleeps.Add, attempts: 2, retryDelayMs: 0));
        Assert.AreEqual(2, calls);
        Assert.AreEqual(0, sleeps.Count, "No pause asked for, none taken.");
    }

    [TestMethod]
    public void ClipboardPrivacyPublishesDocumentedExclusionFormats()
    {
        Assert.AreEqual(3, ClipboardPrivacy.FormatNames.Count);
        Assert.IsTrue(ClipboardPrivacy.FormatNames.Contains("ExcludeClipboardContentFromMonitorProcessing"));
        Assert.IsTrue(ClipboardPrivacy.FormatNames.Contains("CanIncludeInClipboardHistory"));
        Assert.IsTrue(ClipboardPrivacy.FormatNames.Contains("CanUploadToCloudClipboard"));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, ClipboardPrivacy.DenyPayload());
        Assert.AreNotSame(ClipboardPrivacy.DenyPayload(), ClipboardPrivacy.DenyPayload());
    }

    [TestMethod]
    public void RegisteredClipboardFormatsArePreservedWithText()
    {
        const uint protectedMark = 0xC001;
        const uint protectedMark2 = 0xC002;
        Assert.AreEqual(ClipboardFormatHandling.Copy, ClipboardFormats.Classify(protectedMark));
        IReadOnlyList<uint>? plan = ClipboardFormats.PlanCapture([ClipboardFormats.CF_UNICODETEXT, protectedMark, protectedMark2]);
        Assert.IsNotNull(plan);
        Assert.IsTrue(plan.Contains(protectedMark));
        Assert.IsTrue(plan.Contains(protectedMark2));
    }

    [TestMethod]
    public void TheRestoredClipboardIsMarkedSoItIsNotKeptAsANewCopy()
    {
        // Windows takes the restore for a new copy: every pasted expansion added the user's last copy to
        // Clipboard History again and synced it to Cloud Clipboard again.
        var registered = new Dictionary<string, uint>
        {
            [ClipboardPrivacy.ExcludeFromMonitorProcessing] = 0xC0A0,
            [ClipboardPrivacy.CanIncludeInClipboardHistory] = 0xC0A1,
            [ClipboardPrivacy.CanUploadToCloudClipboard] = 0xC0A2,
        };
        var text = new ClipboardEntry(ClipboardFormats.CF_UNICODETEXT, [65, 0, 0, 0]);

        Assert.IsTrue(ClipboardPrivacy.ShouldMarkRestore([text], name => registered[name]));

        // A copy that came with markings of its own goes back with them, whatever they say.
        foreach (uint mark in registered.Values)
        {
            Assert.IsFalse(ClipboardPrivacy.ShouldMarkRestore([text, new ClipboardEntry(mark, [1, 0, 0, 0])], name => registered[name]), $"0x{mark:X}");
        }

        Assert.IsFalse(ClipboardPrivacy.ShouldMarkRestore([], name => registered[name]), "An empty clipboard stays empty.");
    }

    [TestMethod]
    public void TheClipboardIsHandedBackByTheClockNotByCountingShortSleeps()
    {
        // A 5 ms Thread.Sleep lasts a whole timer tick, about 15 ms, so counting polls held the clipboard about
        // three times as long as intended: over a second instead of 350 ms.
        const int timerTick = 15;
        long now = 0;
        void Sleep(int ms) => now += Math.Max(ms, timerTick);

        Assert.IsTrue(Injector.WaitForPasteToSettle(() => false, Sleep, () => now));
        Assert.AreEqual(360, now, "Never read: the 350 ms budget, to the next tick.");

        // Opened by the target at the first check and closed by the next.
        now = 0;
        int checks = 0;
        Assert.IsTrue(Injector.WaitForPasteToSettle(() => ++checks == 1, Sleep, () => now));
        Assert.AreEqual(170, now, "Read: the 150 ms floor, then the 20 ms grace period.");
    }

    [TestMethod]
    public void TheClipboardWaitsForATargetThatIsntTakingMessages()
    {
        // A target busy for 800 ms still had the Ctrl+V queued when the clipboard went back at 350 ms, so it pasted what
        // the user had copied instead of the snippet.
        const int timerTick = 15;
        long now = 0;
        void Sleep(int ms) => now += Math.Max(ms, timerTick);

        Assert.IsTrue(Injector.WaitForPasteToSettle(() => false, Sleep, () => now, () => now >= 800));
        Assert.AreEqual(960, now, "Busy until 810 ms, then the 150 ms floor for its Ctrl+V.");

        // It reads the paste as soon as it answers again, and has it once the clipboard is closed.
        now = 0;
        bool read = false;
        bool ReadOnceAnswering()
        {
            if (read || now < 800)
            {
                return false;
            }

            read = true;
            return true;
        }

        Assert.IsTrue(Injector.WaitForPasteToSettle(ReadOnceAnswering, Sleep, () => now, () => now >= 800));
        Assert.AreEqual(845, now, "Read at 810 ms, closed by 825 ms, then the grace period.");

        // Still busy at the cap: the snippet stays, since its paste may yet come.
        now = 0;
        Assert.IsFalse(Injector.WaitForPasteToSettle(() => false, Sleep, () => now, () => false));
        Assert.AreEqual(5010, now, "The 5 s cap, to the next tick.");

        // A target that's taking messages but not seen reading gets the clipboard back at the budget, as before.
        now = 0;
        Assert.IsTrue(Injector.WaitForPasteToSettle(() => false, Sleep, () => now, () => true));
        Assert.AreEqual(360, now);
    }

    [TestMethod]
    public void TheGuardIsKeptOnWhileTheClipboardWaits()
    {
        // Busy for 3 s, between the guard's 2 s and the 5 s cap: with nothing of ours sent meanwhile, the guard let go of
        // the keys it held while the clipboard still had the snippet. It's kept on at every check.
        const int timerTick = 15;
        long now = 0;
        var keptOn = new List<long>();
        Assert.IsTrue(Injector.WaitForPasteToSettle(() => false, ms => now += Math.Max(ms, timerTick), () => now, () => now >= 3000, () => keptOn.Add(now)));
        Assert.AreEqual(3150, now, "Busy until 3 s, then the 150 ms floor for its Ctrl+V.");
        Assert.AreEqual(0L, keptOn.FirstOrDefault(-1));
        Assert.AreEqual(now, keptOn.LastOrDefault(-1));
        Assert.IsTrue(keptOn.Zip(keptOn.Skip(1)).All(pair => pair.Second - pair.First <= timerTick), "Never longer apart than a check.");
    }

    [TestMethod]
    public void ThePastesCtrlIsHeldUntilTheTargetReadsOrTheClipboardWouldGoBack()
    {
        // Windows 11 Notepad, still busy with the erase, got to the V 70 to 290 ms after it was sent and read Ctrl as it was
        // then. Ctrl stays down until the target is seen reading the clipboard, and no longer than the settle waits for it.
        const int timerTick = 15;
        long now = 0;
        void Sleep(int ms) => now += Math.Max(ms, timerTick);

        Assert.AreEqual(new PasteHold(ReadSeen: false, 360), Injector.HoldForRead(() => false, () => true, Sleep, () => now), "Never seen: the 350 ms budget, to the next tick.");

        now = 0;
        Assert.AreEqual(new PasteHold(ReadSeen: true, 90), Injector.HoldForRead(() => now >= 80, () => true, Sleep, () => now), "Seen at the first check after the read.");

        // A key of the user's could now reach the app with Ctrl down: let go at once.
        now = 0;
        Assert.AreEqual(new PasteHold(ReadSeen: false, 30), Injector.HoldForRead(() => false, () => now < 30, Sleep, () => now));
    }

    [TestMethod]
    public void TheSettleCarriesOnFromThePastesCtrlHold()
    {
        // Timed from the V, as the hold was. Held to the budget unseen, the clipboard goes back at once rather than after
        // another 350 ms, so the hold adds nothing to the borrow.
        const int timerTick = 15;
        long now = 360;
        void Sleep(int ms) => now += Math.Max(ms, timerTick);

        Assert.IsTrue(Injector.WaitForPasteToSettle(() => false, Sleep, () => now, () => true));
        Assert.AreEqual(360, now);

        // A read seen during the hold counts: the floor, counted from the V, then the grace period.
        now = 90;
        Assert.IsTrue(Injector.WaitForPasteToSettle(() => false, Sleep, () => now, readSeen: true));
        Assert.AreEqual(170, now);
    }
}
