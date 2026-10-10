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
    public void ModernColdBootstrapDoesNotChangeLegacyOrHostBudget()
    {
        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(now.AddMinutes(15), SandboxTimeouts.BootstrapDeadline(now, now.AddHours(2), modern: false));
        Assert.AreEqual(now.AddMinutes(35), SandboxTimeouts.BootstrapDeadline(now, now.AddHours(2), modern: true));
        Assert.AreEqual(TimeSpan.FromMinutes(40), SandboxTimeouts.LegacyRun);
        Assert.IsTrue(SandboxTimeouts.ModernStart < SandboxTimeouts.ModernBootstrap);
        Assert.IsTrue(SandboxTimeouts.ModernBootstrap + SandboxTimeouts.LegacyBootstrap < SandboxTimeouts.ModernRun);
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
