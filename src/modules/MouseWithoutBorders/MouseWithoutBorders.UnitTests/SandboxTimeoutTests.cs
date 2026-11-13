// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[OSCondition(OperatingSystems.Windows)]
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
        Assert.IsLessThan(SandboxTimeouts.ModernBootstrap, SandboxTimeouts.ModernStart);
        Assert.IsLessThan(SandboxTimeouts.ModernRun, SandboxTimeouts.ModernBootstrap + SandboxTimeouts.LegacyBootstrap);
    }

    [TestMethod]
    [DataRow(SandboxTimeouts.EndpointStartupMinutes, SandboxTimeouts.EndpointStartRequestSeconds)]
    public void EndpointStartupHasFifteenMinutesAndSeparateReplyHeadroom(int startupMinutes, int requestSeconds)
    {
        Assert.AreEqual(15, startupMinutes);
        Assert.AreEqual(1200, requestSeconds);
        Assert.IsGreaterThan(startupMinutes * 60, requestSeconds);
        Assert.AreEqual(TimeSpan.FromMinutes(23), SandboxTimeouts.RecoveryRun);
    }

    [TestMethod]
    [DataRow(SandboxTimeouts.GuestStopRequestSeconds)]
    public void GuestCleanupAndLegacyViewerHaveIndependentReadinessBudgets(int guestStopRequestSeconds)
    {
        Assert.AreEqual(300, guestStopRequestSeconds);
        Assert.AreEqual(TimeSpan.FromMinutes(3), SandboxTimeouts.LegacyViewerStart);
        Assert.IsLessThan(SandboxTimeouts.LegacyBootstrap, SandboxTimeouts.LegacyViewerStart);
    }

    [TestMethod]
    [DataRow(SandboxTimeouts.EndpointEvidenceRequestSeconds, SandboxTimeouts.GuestStopRequestSeconds)]
    public void EvidenceCollectionHasItsOwnBoundedRequestBudget(int evidenceRequestSeconds, int guestStopRequestSeconds)
    {
        Assert.AreEqual(180, evidenceRequestSeconds);
        Assert.IsLessThan(guestStopRequestSeconds, evidenceRequestSeconds);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RunBudgetEnclosesBootstrapAndBothEndpointStartRequests(bool modern)
    {
        var bootstrap = modern ? SandboxTimeouts.ModernBootstrap : SandboxTimeouts.LegacyBootstrap;
        var run = modern ? SandboxTimeouts.ModernRun : SandboxTimeouts.LegacyRun;
        Assert.IsGreaterThan(bootstrap + TimeSpan.FromSeconds(2 * SandboxTimeouts.EndpointStartRequestSeconds), run);
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
