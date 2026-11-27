// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
    public void PersistentCopy_RestoresAfterFailureBeforeAllowingAnotherCopy()
    {
        var held = false;
        string? text = "A";
        var result = Win32Clipboard.WriteText(
            null,
            () =>
            {
                text = "B";
                held = true;
                return (true, true);
            },
            () => throw new AssertFailedException("A Copy captures the state while it has the clipboard open."),
            () =>
            {
                Assert.IsTrue(held);
                var previous = text;
                return Win32Clipboard.CommitOpenedWrite(
                    empty: () =>
                    {
                        Assert.IsTrue(held);
                        text = null;
                        return true;
                    },
                    publish: () => false,
                    restore: () =>
                    {
                        Assert.IsTrue(held);
                        text = previous;
                        return true;
                    });
            },
            () =>
            {
                Assert.AreEqual("B", text, "Restore the copy that was present when the clipboard was acquired.");
                held = false;
                text = "C";
            });

        Assert.AreEqual(ClipboardWrite.Refused, result);
        Assert.AreEqual("C", text, "A later copy remains on the clipboard.");
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

        Assert.AreEqual((terminal ? InsertOutcome.TerminalNeedsPaste : InsertOutcome.Inserted, false), result);
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

    [TestMethod]
    public void CopyCommand_EmptyExpansionKeepsOriginalClipboardAndDoesNotComplete()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!email", "{{clipboard:email}}"));
        environment.Clipboard.Text = "No email address here";
        var completions = 0;
        var copy = environment.Actions.CopyCommand(environment.SnippetFor("!email"), answers: null, copied: () => completions++);

        var result = copy.Invoke();

        Assert.AreEqual("No email address here", environment.Clipboard.Text);
        Assert.AreEqual(0, environment.Clipboard.Writes.Count);
        Assert.AreEqual(0, completions);
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = (ToastArgs)result.Args!;
        Assert.AreEqual(Resources.picks_comes_out_empty, toast.Message);
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
    }

    [TestMethod]
    public void CopyCommand_UnsupportedClipboardIsLeftAlone()
    {
        var clipboard = new FakeClipboard { Text = "Original", CanBeSaved = false };
        var originalSequence = clipboard.SequenceNumber;
        var copy = new CopySnippetCommand(() => "Replacement", clipboard);

        var result = copy.Invoke();

        Assert.AreEqual("Original", clipboard.Text);
        Assert.AreEqual(originalSequence, clipboard.SequenceNumber);
        Assert.AreEqual(Resources.copy_failed_toast, ((ToastArgs)result.Args!).Message);
    }

    [TestMethod]
    public void CopyCommand_ReportsAnIncompleteRollback()
    {
        var clipboard = new FakeClipboard { Text = "Original", NextWrite = ClipboardWrite.Failed, FailingRestores = 1 };
        var copy = new CopySnippetCommand(() => "Replacement", clipboard);

        var result = copy.Invoke();

        var toast = (ToastArgs)result.Args!;
        StringAssert.Contains(toast.Message, Resources.toast_clipboard_not_restored);
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
    }
}
