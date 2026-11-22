// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public class ClipboardHistoryDeleterTests
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

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

    private static Exception CantOpen() => Marshal.GetExceptionForHR(ClipboardHistoryDeleter.ClipboardCantOpenHResult)!;
}
