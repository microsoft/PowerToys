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
}
