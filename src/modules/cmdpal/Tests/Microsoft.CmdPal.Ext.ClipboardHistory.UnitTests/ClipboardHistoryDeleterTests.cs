// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public class ClipboardHistoryDeleterTests
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly DateTimeOffset SetTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly PastedContent TextPaste = new(ClipboardFormat.Text, "hello");
    private static readonly string[] OriginalAndNew = ["original", "new"];
    private static readonly string[] OriginalAndBitmap = ["original", "bitmap"];

    [TestMethod]
    public void RetriesWithBackoff_WhenClipboardCannotBeOpened()
    {
        var attempts = 0;
        var sleeps = new List<TimeSpan>();
        var warnings = new List<string>();

        var result = ClipboardHistoryDeleter.TryDeleteWithRetry(
            () => ++attempts < 3 ? throw CantOpen() : true,
            sleeps.Add,
            MaxAttempts,
            RetryDelay,
            warnings.Add);

        Assert.IsTrue(result);
        Assert.AreEqual(3, attempts);
        CollectionAssert.AreEqual(new[] { RetryDelay, RetryDelay * 2 }, sleeps);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void LogsAndReturnsFalse_WhenClipboardNeverOpens()
    {
        var attempts = 0;
        var warnings = new List<string>();

        var result = ClipboardHistoryDeleter.TryDeleteWithRetry(
            () =>
            {
                attempts++;
                throw CantOpen();
            },
            _ => { },
            MaxAttempts,
            RetryDelay,
            warnings.Add);

        Assert.IsFalse(result);
        Assert.AreEqual(MaxAttempts, attempts);
        Assert.AreEqual(1, warnings.Count);
    }

    [TestMethod]
    public void DoesNotRetry_OnOtherExceptions()
    {
        var attempts = 0;
        var warnings = new List<string>();

        var result = ClipboardHistoryDeleter.TryDeleteWithRetry(
            () =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            },
            _ => Assert.Fail("Should not back off for non-clipboard-lock errors"),
            MaxAttempts,
            RetryDelay,
            warnings.Add);

        Assert.IsFalse(result);
        Assert.AreEqual(1, attempts);
        Assert.AreEqual(1, warnings.Count);
    }

    [TestMethod]
    public void SelectsOriginalAndNewestMatchingNewEntry()
    {
        var history = new[]
        {
            Entry("other-new", 300, "different"),
            Entry("new-older", 100, "hello"),
            Entry("new", 200, "hello"),
            Entry("before-paste", -1000, "hello"),
            Entry("original", -5000, "hello"),
        };

        var targets = ClipboardHistoryDeleter.SelectEntriesToDelete(history, "original", TextPaste, SetTime, out var foundNewEntry);

        Assert.IsTrue(foundNewEntry);
        CollectionAssert.AreEquivalent(OriginalAndNew, targets.ToArray());
    }

    [TestMethod]
    public void MatchesImagePasteByBitmapOnly()
    {
        var history = new[]
        {
            Entry("text", 300, "hello"),
            Entry("bitmap", 200, null, hasBitmap: true),
            Entry("original", -5000, null, hasBitmap: true),
        };

        var targets = ClipboardHistoryDeleter.SelectEntriesToDelete(history, "original", new PastedContent(ClipboardFormat.Image, null), SetTime, out var foundNewEntry);

        Assert.IsTrue(foundNewEntry);
        CollectionAssert.AreEquivalent(OriginalAndBitmap, targets.ToArray());
    }

    [TestMethod]
    public void RemovePastedEntries_WaitsForNewEntryAndDeletesBoth()
    {
        var history = new List<ClipboardHistoryEntry> { Entry("original", -5000, "hello") };
        var reads = 0;
        var deleted = new List<string>();
        var warnings = new List<string>();

        var result = ClipboardHistoryDeleter.RemovePastedEntries(
            () =>
            {
                // The history service adds the new entry asynchronously, after the first read.
                if (++reads == 2)
                {
                    history.Insert(0, Entry("new", 250, "hello"));
                }

                return history.ToArray();
            },
            id =>
            {
                deleted.Add(id);
                return history.RemoveAll(e => e.Id == id) > 0;
            },
            _ => { },
            "original",
            TextPaste,
            SetTime,
            warnings.Add);

        Assert.IsTrue(result);
        CollectionAssert.AreEquivalent(OriginalAndNew, deleted);
        Assert.AreEqual(0, history.Count);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void RemovePastedEntries_WarnsWhenEntryIsStillPresent()
    {
        var history = new[] { Entry("new", 250, "hello"), Entry("original", -5000, "hello") };
        var warnings = new List<string>();

        // DeleteItemFromHistory can report success without removing anything.
        var result = ClipboardHistoryDeleter.RemovePastedEntries(
            () => history,
            _ => true,
            _ => { },
            "original",
            TextPaste,
            SetTime,
            warnings.Add);

        Assert.IsFalse(result);
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "still in clipboard history");
    }

    private static ClipboardHistoryEntry Entry(string id, int msAfterSet, string text, bool hasBitmap = false) =>
        new(id, SetTime.AddMilliseconds(msAfterSet), text, hasBitmap);

    private static Exception CantOpen() => Marshal.GetExceptionForHR(ClipboardHistoryDeleter.ClipboardCantOpenHResult)!;
}
