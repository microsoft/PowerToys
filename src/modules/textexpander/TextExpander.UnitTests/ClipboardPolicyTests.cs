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
        Assert.AreEqual(43U, loan.Expected);
    }

    [TestMethod]
    public void ClipboardLoanRearmKeepsRetriesFromLookingForeign()
    {
        var loan = new ClipboardLoan(sequenceAfterWrite: 41);
        Assert.IsFalse(loan.MayRestore(40));
        loan.Rearm(43);
        Assert.IsTrue(loan.MayRestore(43));
        Assert.IsFalse(loan.MayRestore(44));
        Assert.AreEqual(43U, loan.Expected);
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

        Injector.WaitForPasteToSettle(() => false, Sleep, () => now);
        Assert.AreEqual(360, now, "Never read: the 350 ms budget, to the next tick.");

        // Opened by the target at the first check and closed by the next.
        now = 0;
        int checks = 0;
        Injector.WaitForPasteToSettle(() => ++checks == 1, Sleep, () => now);
        Assert.AreEqual(170, now, "Read: the 150 ms floor, then the 20 ms grace period.");
    }
}
