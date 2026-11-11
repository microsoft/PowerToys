// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class ClipboardBorrowTests
{
    private static readonly string[] RefusedCalls = ["open", "sequence", "close"];
    private static readonly string[] WriteCalls = ["open", "sequence", "write", "close"];

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Borrow_CopyMadeBeforeOrDuringOpen_IsNotOverwritten(bool copyDuringOpen)
    {
        using var saved = new SavedClipboard([new ClipboardEntry(ClipboardFormats.CF_UNICODETEXT, [65, 0, 0, 0])], 41);
        uint sequence = copyDuringOpen ? 41u : 42u;
        var text = copyDuringOpen ? "A" : "B";
        var held = false;
        var writes = 0;
        var calls = new List<string>();

        var result = Win32Clipboard.WriteText(
            saved,
            () =>
            {
                // The second interleaving copies while OpenClipboard is waiting, before it acquires the lock.
                if (copyDuringOpen)
                {
                    sequence = 42;
                    text = "B";
                }

                held = true;
                calls.Add("open");
                return (true, true);
            },
            () =>
            {
                Assert.IsTrue(held, "The captured version must be checked after opening.");
                calls.Add("sequence");
                return sequence;
            },
            () =>
            {
                writes++;
                text = "snippet";
                return ClipboardWrite.Written;
            },
            () =>
            {
                Assert.IsTrue(held);
                calls.Add("close");
                held = false;
            });

        Assert.AreEqual(ClipboardWrite.Refused, result);
        Assert.AreEqual("B", text, "A stale snapshot must never displace the newer copy.");
        Assert.AreEqual(0, writes, "Refuse before EmptyClipboard and publication.");
        Assert.IsFalse(held);
        CollectionAssert.AreEqual(RefusedCalls, calls);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Borrow_UnchangedVersion_WritesWhileOpenAndReturnsTheWriteResult(bool succeeds)
    {
        using var saved = new SavedClipboard([], 41);
        var expected = succeeds ? ClipboardWrite.Written : ClipboardWrite.Failed;
        var held = false;
        var calls = new List<string>();

        var result = Win32Clipboard.WriteText(
            saved,
            () =>
            {
                held = true;
                calls.Add("open");
                return (true, true);
            },
            () =>
            {
                Assert.IsTrue(held);
                calls.Add("sequence");
                return 41;
            },
            () =>
            {
                Assert.IsTrue(held, "The checked version must stay locked through the write.");
                calls.Add("write");
                return expected;
            },
            () =>
            {
                Assert.IsTrue(held);
                calls.Add("close");
                held = false;
            });

        Assert.AreEqual(expected, result);
        Assert.IsFalse(held);
        CollectionAssert.AreEqual(WriteCalls, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Borrow_FailedOrUnownedOpen_NeverWrites(bool opened)
    {
        using var saved = new SavedClipboard([], 41);
        var sequenceReads = 0;
        var writes = 0;
        var closes = 0;

        var result = Win32Clipboard.WriteText(
            saved,
            () => (opened, false),
            () =>
            {
                sequenceReads++;
                return 41;
            },
            () =>
            {
                writes++;
                return ClipboardWrite.Written;
            },
            () => closes++);

        Assert.AreEqual(ClipboardWrite.Refused, result);
        Assert.AreEqual(0, writes);
        Assert.AreEqual(0, sequenceReads);
        Assert.AreEqual(opened ? 1 : 0, closes, "Only an acquired clipboard is closed.");
    }

    [TestMethod]
    public void PersistentCopy_WritesWithoutReadingASnapshotVersion()
    {
        var held = false;
        var sequenceReads = 0;
        var writes = 0;
        var result = Win32Clipboard.WriteText(
            null,
            () =>
            {
                held = true;
                return (true, true);
            },
            () =>
            {
                sequenceReads++;
                return 42;
            },
            () =>
            {
                Assert.IsTrue(held);
                writes++;
                return ClipboardWrite.Written;
            },
            () => held = false);

        Assert.AreEqual(ClipboardWrite.Written, result);
        Assert.AreEqual(1, writes);
        Assert.AreEqual(0, sequenceReads);
        Assert.IsFalse(held);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void Insert_NewerCopySurvivesAndPasteIsNotSent(bool copyDuringOpen, bool terminal)
    {
        const string snippet = "first\nsecond";
        var clipboard = new FakeClipboard { Text = "A" };
        var desktop = new FakeDesktop { AppIsWindowsTerminal = terminal };
        if (copyDuringOpen)
        {
            clipboard.WhileOpeningForWrite = () => clipboard.UserCopies("B");
        }
        else
        {
            clipboard.AfterSave = () => clipboard.UserCopies("B");
        }

        var inserter = new SnippetInserter(clipboard, desktop, new FakeClock());
        var result = inserter.Insert(snippet, InjectionBackend.Clipboard, InjectionPolicy.DefaultClipboardThresholdChars);

        Assert.AreEqual(terminal ? InsertOutcome.TerminalNeedsPaste : InsertOutcome.Inserted, result);
        Assert.AreEqual("B", clipboard.Text);
        Assert.AreEqual(1, clipboard.Saves);
        Assert.AreEqual(("first\r\nsecond", true), clipboard.Writes.Single());
        Assert.AreEqual(0, clipboard.Restores, "Refusing an untouched clipboard must not restore stale A.");
        Assert.IsFalse(desktop.Pasted);
        if (terminal)
        {
            Assert.AreEqual(0, desktop.Sent.Count, "Multiline fallback must not execute terminal input.");
        }
        else
        {
            Assert.AreEqual(snippet, desktop.Typed());
        }
    }

    [TestMethod]
    public void CopyCommand_PersistentWriteDoesNotBorrowTheClipboard()
    {
        var clipboard = new FakeClipboard { Text = "A" };
        clipboard.WhileOpeningForWrite = () => clipboard.UserCopies("B");
        var command = new CopySnippetCommand(() => "first\nsecond", clipboard);

        _ = command.Invoke();

        Assert.AreEqual("first\r\nsecond", clipboard.Text);
        Assert.AreEqual(("first\r\nsecond", false), clipboard.Writes.Single());
        Assert.AreEqual(0, clipboard.Saves);
        Assert.AreEqual(0, clipboard.Restores);
    }
}
