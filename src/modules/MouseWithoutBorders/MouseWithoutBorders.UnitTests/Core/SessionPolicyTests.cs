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
    public void ExplicitOptInOnlyEnablesDebugUserDesktop(string desktopName)
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
    [DataRow(true, false, "default")]
    [DataRow(false, true, "default")]
    [DataRow(true, true, "default")]
    [DataRow(false, false, "winlogon")]
    [DataRow(false, false, "Screen-saver")]
    [DataRow(false, false, "disconnect")]
    [DataRow(false, false, "")]
    [DataRow(false, false, null)]
    public void OptInDoesNotRelaxServiceSystemOrOtherDesktops(bool serviceMode, bool runningAsSystem, string? desktopName)
    {
        var policy = SessionPolicy.FromConfiguration("1", serviceMode, runningAsSystem, desktopName!);

        Assert.IsFalse(policy.AllowNonConsole);
        Assert.IsFalse(policy.IsSessionAllowed(5, 1));
        Assert.IsFalse(policy.IsSessionAllowed(1, uint.MaxValue));
        Assert.IsTrue(policy.IsSessionAllowed(1, 1));
    }
}
