// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class SessionPolicyTests
{
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("0")]
    [DataRow("true")]
    [DataRow("1 ")]
    [DataRow(" 1")]
    public void NonConsoleSessionsRequireExplicitOptIn(string? flagValue)
    {
        var policy = SessionPolicy.FromConfiguration(flagValue!, serviceMode: false, runningAsSystem: false, "default");

        Assert.IsFalse(policy.AllowNonConsole);
        Assert.IsTrue(policy.IsSessionAllowed(1, 1));
        Assert.IsFalse(policy.IsSessionAllowed(5, 1));
        Assert.IsFalse(policy.IsSessionAllowed(1, uint.MaxValue));
    }

    [DataTestMethod]
    [DataRow("default")]
    [DataRow("Default")]
    public void EnvironmentOptInOnlyEnablesDebugUserDesktop(string desktopName)
    {
        var policy = SessionPolicy.FromConfiguration("1", serviceMode: false, runningAsSystem: false, desktopName);
#if DEBUG
        Assert.IsTrue(policy.AllowNonConsole);
        Assert.IsTrue(policy.IsSessionAllowed(5, 1));
        Assert.IsTrue(policy.IsSessionAllowed(1, uint.MaxValue));
#else
        Assert.IsFalse(policy.AllowNonConsole);
        Assert.IsFalse(policy.IsSessionAllowed(5, 1));
        Assert.IsFalse(policy.IsSessionAllowed(1, uint.MaxValue));
#endif
        Assert.IsTrue(policy.IsSessionAllowed(1, 1));
        Assert.IsFalse(policy.IsSessionAllowed(-1, uint.MaxValue));
    }

    [DataTestMethod]
    [DataRow("default")]
    [DataRow("Default")]
    [DataRow("DEFAULT")]
    public void JsonOptInEnablesUserDefaultDesktop(string desktopName)
    {
        var policy = SessionPolicy.FromConfiguration(null!, serviceMode: false, runningAsSystem: false, desktopName, allowNonConsoleSessions: true);

        Assert.IsTrue(policy.AllowNonConsole);
        Assert.IsTrue(policy.IsSessionAllowed(5, 1));
        Assert.IsTrue(policy.IsSessionAllowed(1, uint.MaxValue));
        Assert.IsTrue(policy.IsSessionAllowed(1, 1));
    }

    [TestMethod]
    public void JsonFalseKeepsConsoleRestriction()
    {
        var policy = SessionPolicy.FromConfiguration(null!, serviceMode: false, runningAsSystem: false, "default", allowNonConsoleSessions: false);

        Assert.IsFalse(policy.AllowNonConsole);
        Assert.IsTrue(policy.IsSessionAllowed(1, 1));
        Assert.IsFalse(policy.IsSessionAllowed(5, 1));
        Assert.IsFalse(policy.IsSessionAllowed(1, uint.MaxValue));
    }

    [DataTestMethod]
    [DataRow(true, false, "default", false)]
    [DataRow(true, false, "default", true)]
    [DataRow(false, true, "default", false)]
    [DataRow(false, true, "default", true)]
    [DataRow(true, true, "default", false)]
    [DataRow(true, true, "default", true)]
    [DataRow(false, false, "winlogon", false)]
    [DataRow(false, false, "winlogon", true)]
    [DataRow(false, false, "Screen-saver", false)]
    [DataRow(false, false, "Screen-saver", true)]
    [DataRow(false, false, "disconnect", false)]
    [DataRow(false, false, "disconnect", true)]
    [DataRow(false, false, "", false)]
    [DataRow(false, false, "", true)]
    [DataRow(false, false, null, false)]
    [DataRow(false, false, null, true)]
    public void OptInDoesNotRelaxServiceSystemOrOtherDesktops(bool serviceMode, bool runningAsSystem, string? desktopName, bool allowNonConsoleSessions)
    {
        var policy = SessionPolicy.FromConfiguration("1", serviceMode, runningAsSystem, desktopName!, allowNonConsoleSessions);

        Assert.IsFalse(policy.AllowNonConsole);
        Assert.IsFalse(policy.IsSessionAllowed(5, 1));
        Assert.IsFalse(policy.IsSessionAllowed(1, uint.MaxValue));
        Assert.IsTrue(policy.IsSessionAllowed(1, 1));
    }

    [DataTestMethod]
    [DataRow(-1, false)]
    [DataRow(-1, true)]
    [DataRow(-2, false)]
    [DataRow(-2, true)]
    [DataRow(int.MinValue, false)]
    [DataRow(int.MinValue, true)]
    public void NegativeSessionIdsAreNeverAllowed(int sessionId, bool allowNonConsoleSessions)
    {
        var policy = SessionPolicy.FromConfiguration("1", serviceMode: false, runningAsSystem: false, "default", allowNonConsoleSessions);

        Assert.IsFalse(policy.IsSessionAllowed(sessionId, 1));
        Assert.IsFalse(policy.IsSessionAllowed(sessionId, uint.MaxValue));
    }
}
