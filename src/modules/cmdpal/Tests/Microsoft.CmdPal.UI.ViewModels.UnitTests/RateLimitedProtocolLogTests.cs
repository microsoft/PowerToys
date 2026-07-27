// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class RateLimitedProtocolLogTests
{
    [TestMethod]
    public void Run_EmitsUpToBudget_ThenSuppressesWithinWindow()
    {
        long now = 1000;
        var emitted = 0;
        var log = new RateLimitedProtocolLog(3, TimeSpan.FromMilliseconds(100), static _ => { }, () => now);

        for (var i = 0; i < 10; i++)
        {
            log.Run(() => emitted++);
        }

        Assert.AreEqual(3, emitted, "Only the per-window budget of entries should be emitted.");
        Assert.AreEqual(7, log.TotalSuppressed, "Every entry beyond the budget must be counted as suppressed.");
    }

    [TestMethod]
    public void Run_NewWindow_ReportsPreviousWindowSuppressedSummary_AndResetsBudget()
    {
        long now = 5000;
        var emitted = 0;
        long reportedSuppressed = -1;
        var log = new RateLimitedProtocolLog(2, TimeSpan.FromMilliseconds(100), s => reportedSuppressed = s, () => now);

        // Exhaust the first window: 2 emitted, 3 suppressed.
        for (var i = 0; i < 5; i++)
        {
            log.Run(() => emitted++);
        }

        Assert.AreEqual(2, emitted);
        Assert.AreEqual(-1, reportedSuppressed, "No summary should be reported until a new window begins.");

        // Advance past the window boundary. The next call reports the previous window's suppressed count
        // and emits under a fresh budget.
        now += 100;
        log.Run(() => emitted++);

        Assert.AreEqual(3, emitted, "The budget must reset when a new window begins.");
        Assert.AreEqual(3, reportedSuppressed, "The suppressed-count summary for the previous window must be reported.");
        Assert.AreEqual(3, log.TotalSuppressed, "The running total of suppressed entries is preserved across windows.");
    }

    [TestMethod]
    public void Run_DoesNotReportSummary_WhenNothingWasSuppressed()
    {
        long now = 0;
        var summaryCallbacks = 0;
        var log = new RateLimitedProtocolLog(5, TimeSpan.FromMilliseconds(50), _ => summaryCallbacks++, () => now);

        log.Run(static () => { });
        now += 50;
        log.Run(static () => { });

        Assert.AreEqual(0, summaryCallbacks, "A window that suppressed nothing must not trigger a summary callback.");
        Assert.AreEqual(0, log.TotalSuppressed);
    }

    [TestMethod]
    public void TotalSuppressed_StaysAccurate_UnderSustainedFlood()
    {
        long now = 0;
        var log = new RateLimitedProtocolLog(1, TimeSpan.FromMilliseconds(10), static _ => { }, () => now);

        // Many entries within a single window: one emits, the rest are suppressed. The limiter holds only
        // fixed counters, so its accounting stays exact no matter how large the flood is.
        for (var i = 0; i < 100_000; i++)
        {
            log.Run(static () => { });
        }

        Assert.AreEqual(99_999, log.TotalSuppressed);
    }

    [TestMethod]
    public void Constructor_ClampsNonPositiveBudget_ToAtLeastOne()
    {
        long now = 0;
        var emitted = 0;
        var log = new RateLimitedProtocolLog(0, TimeSpan.FromMilliseconds(100), static _ => { }, () => now);

        log.Run(() => emitted++);
        log.Run(() => emitted++);

        Assert.AreEqual(1, emitted, "A non-positive budget must be clamped to one so at least one entry is emitted per window.");
    }

    [TestMethod]
    public void Constructor_NullSummaryCallback_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new RateLimitedProtocolLog(1, TimeSpan.FromSeconds(1), null!));
    }

    [TestMethod]
    public void Run_NullEmit_Throws()
    {
        var log = new RateLimitedProtocolLog(1, TimeSpan.FromSeconds(1), static _ => { });

        Assert.ThrowsException<ArgumentNullException>(() => log.Run(null!));
    }
}
