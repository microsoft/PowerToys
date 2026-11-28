// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class ClipboardBorrowTests
{
    private static readonly string[] CaptureCalls = ["read 13", "read 1", "sequence"];
    private static readonly string[] WriteCalls = ["open", "sequence", "write", "close"];

    [TestMethod]
    public void Capture_RecordsTheVersionAfterDelayedFormatsHaveRendered()
    {
        uint sequence = 10;
        var calls = new List<string>();
        using var snapshot = Clipboard.Capture(
            [ClipboardFormats.CF_UNICODETEXT, ClipboardFormats.CF_TEXT],
            (format, _) =>
            {
                calls.Add($"read {format}");
                sequence++;
                return [1, 0];
            },
            () =>
            {
                calls.Add("sequence");
                return sequence;
            });

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(12u, snapshot.SequenceNumber);
        CollectionAssert.AreEqual(CaptureCalls, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SetText_NewCopyBeforeTheWrite_LeavesItUntouched(bool copyWhileOpening)
    {
        var text = "Original copy";
        uint sequence = 10;
        using var snapshot = Clipboard.Capture(
            [ClipboardFormats.CF_UNICODETEXT],
            (_, _) => Encoding.Unicode.GetBytes(text + '\0'),
            () => sequence);
        Assert.IsNotNull(snapshot);

        void Copy()
        {
            text = "Newer copy";
            sequence++;
        }

        if (!copyWhileOpening)
        {
            // Snapshot has returned and closed the clipboard; a newer copy must not inherit its saved version.
            Copy();
        }

        var heldOpen = false;
        var writes = 0;
        var closes = 0;
        var result = Clipboard.SetText(
            snapshot,
            () =>
            {
                if (copyWhileOpening)
                {
                    // The native open can wait for an app that is in the middle of copying.
                    Copy();
                }

                heldOpen = true;
                return ClipboardOpen.Owned;
            },
            () =>
            {
                Assert.IsTrue(heldOpen, "The version must be checked after opening.");
                return sequence;
            },
            () =>
            {
                Assert.IsTrue(heldOpen);
                writes++;
                text = "Snippet";
                return ClipboardWrite.Written;
            },
            () =>
            {
                heldOpen = false;
                closes++;
            });

        Assert.AreEqual(ClipboardWrite.Refused, result);
        Assert.AreEqual(10u, snapshot.SequenceNumber);
        Assert.AreEqual("Newer copy", text);
        Assert.AreEqual(0, writes, "Neither EmptyClipboard nor publication may run over the newer copy.");
        Assert.AreEqual(1, closes);
        Assert.IsFalse(heldOpen);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SetText_UnchangedSnapshot_WritesWhileOpenAndReturnsTheWriteOutcome(bool completeWrite)
    {
        using var snapshot = new ClipboardSnapshot([], 10);
        var calls = new List<string>();
        var outcome = completeWrite ? ClipboardWrite.Written : ClipboardWrite.Failed;

        var result = Clipboard.SetText(
            snapshot,
            () =>
            {
                calls.Add("open");
                return ClipboardOpen.Owned;
            },
            () =>
            {
                calls.Add("sequence");
                return 10;
            },
            () =>
            {
                calls.Add("write");
                return outcome;
            },
            () => calls.Add("close"));

        Assert.AreEqual(outcome, result);
        CollectionAssert.AreEqual(WriteCalls, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SetText_ClipboardCannotBeOpenedForWriting_Refuses(bool openedWithoutOwner)
    {
        using var snapshot = new ClipboardSnapshot([], 10);
        var writes = 0;
        var versionReads = 0;
        var closes = 0;

        var result = Clipboard.SetText(
            snapshot,
            () => openedWithoutOwner ? ClipboardOpen.Unowned : ClipboardOpen.Failed,
            () =>
            {
                versionReads++;
                return 10;
            },
            () =>
            {
                writes++;
                return ClipboardWrite.Written;
            },
            () => closes++);

        Assert.AreEqual(ClipboardWrite.Refused, result);
        Assert.AreEqual(0, writes);
        Assert.AreEqual(0, versionReads);
        Assert.AreEqual(openedWithoutOwner ? 1 : 0, closes);
    }
}
