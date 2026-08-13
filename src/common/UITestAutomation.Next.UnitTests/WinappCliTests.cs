// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class WinappCliTests
{
    private string? originalInvokeTimeout;

    [TestInitialize]
    public void SaveEnvironment()
    {
        originalInvokeTimeout = Environment.GetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable);
    }

    [TestCleanup]
    public void RestoreEnvironment()
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, originalInvokeTimeout);
    }

    [TestMethod]
    public void ResolveInvokeTimeoutHonorsEnvironmentOverride()
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, "180");

        Assert.AreEqual(TimeSpan.FromSeconds(180), WinappCli.ResolveInvokeTimeout([]));
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("0")]
    [DataRow("3601")]
    public void ResolveInvokeTimeoutRejectsInvalidEnvironmentOverride(string value)
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, value);

        Assert.AreEqual(TimeSpan.FromSeconds(60), WinappCli.ResolveInvokeTimeout([]));
    }

    [TestMethod]
    public void ResolveInvokeTimeoutExtendsPastLongerCommandTimeout()
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, "180");

        Assert.AreEqual(
            TimeSpan.FromSeconds(230),
            WinappCli.ResolveInvokeTimeout(["wait-for", "target", "--timeout", "200000"]));
    }
}
