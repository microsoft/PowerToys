// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SandboxTimeoutTests
{
    [TestMethod]
    public void BootstrapBudgetsRemainBackendSpecific()
    {
        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(now.AddMinutes(20), SandboxTimeouts.BootstrapDeadline(now, now.AddHours(2), modern: false));
        Assert.AreEqual(now.AddMinutes(35), SandboxTimeouts.BootstrapDeadline(now, now.AddHours(2), modern: true));
        Assert.AreEqual(TimeSpan.FromMinutes(65), SandboxTimeouts.LegacyRun);
        Assert.AreEqual(TimeSpan.FromMinutes(90), SandboxTimeouts.ModernRun);
        Assert.AreEqual(TimeSpan.FromMinutes(15), SandboxTimeouts.ModernStart);
        Assert.IsTrue(SandboxTimeouts.ModernStart < SandboxTimeouts.ModernBootstrap);
        Assert.IsTrue(SandboxTimeouts.ModernBootstrap + SandboxTimeouts.LegacyBootstrap < SandboxTimeouts.ModernRun);
    }

    [TestMethod]
    public void EndpointStartupHasFifteenMinutesAndSeparateReplyHeadroom()
    {
        Assert.AreEqual(15, SandboxTimeouts.EndpointStartupMinutes);
        Assert.AreEqual(1200, SandboxTimeouts.EndpointStartRequestSeconds);
        Assert.IsTrue(SandboxTimeouts.EndpointStartRequestSeconds > SandboxTimeouts.EndpointStartupMinutes * 60);
        Assert.AreEqual(TimeSpan.FromMinutes(23), SandboxTimeouts.RecoveryRun);
    }

    [TestMethod]
    public void GuestCleanupAndLegacyViewerHaveIndependentReadinessBudgets()
    {
        Assert.AreEqual(300, SandboxTimeouts.GuestStopRequestSeconds);
        Assert.AreEqual(TimeSpan.FromMinutes(3), SandboxTimeouts.LegacyViewerStart);
        Assert.IsTrue(SandboxTimeouts.LegacyViewerStart < SandboxTimeouts.LegacyBootstrap);
    }

    [TestMethod]
    public void EvidenceCollectionHasItsOwnBoundedRequestBudget()
    {
        Assert.AreEqual(180, SandboxTimeouts.EndpointEvidenceRequestSeconds);
        Assert.IsTrue(SandboxTimeouts.EndpointEvidenceRequestSeconds < SandboxTimeouts.GuestStopRequestSeconds);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RunBudgetEnclosesBootstrapAndBothEndpointStartRequests(bool modern)
    {
        var bootstrap = modern ? SandboxTimeouts.ModernBootstrap : SandboxTimeouts.LegacyBootstrap;
        var run = modern ? SandboxTimeouts.ModernRun : SandboxTimeouts.LegacyRun;
        Assert.IsTrue(run > bootstrap + TimeSpan.FromSeconds(2 * SandboxTimeouts.EndpointStartRequestSeconds));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BootstrapCannotOutliveRunHardDeadline(bool modern)
    {
        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        var hardDeadline = now.AddMinutes(2);
        Assert.AreEqual(hardDeadline, SandboxTimeouts.BootstrapDeadline(now, hardDeadline, modern));
        Assert.ThrowsExactly<WinAppSandboxException>(() => SandboxTimeouts.BootstrapDeadline(now, now, modern));
        Assert.ThrowsExactly<WinAppSandboxException>(() => SandboxTimeouts.BootstrapDeadline(now, now.AddSeconds(-1), modern));
    }
}
