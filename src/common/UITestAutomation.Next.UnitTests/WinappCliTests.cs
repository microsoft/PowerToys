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
    private static readonly string[] SetValueWindowOptions = ["ui", "set-value", "-w", "1234"];

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

    [TestMethod]
    [DataRow("--payload discarded")]
    [DataRow("--title \"fixture\" --payload \"argument with spaces\"")]
    [DataRow("--help")]
    [DataRow("-42.5")]
    [DataRow("")]
    [DataRow("ordinary text")]
    public void SetValueSeparatesTargetOptionsFromLiteralArguments(string value)
    {
        var textBox = new TextBox
        {
            Owner = new Session(PowerToysModule.Workspaces, 1234, "Fixture", 1, "Fixture"),
            Selector = "CommandLineTextBox",
        };

        var arguments = textBox.BuildSetValueArguments(value);
        var separator = Array.IndexOf(arguments, "--");

        Assert.IsTrue(separator > 0, "Literal values must follow an end-of-options separator.");
        CollectionAssert.AreEqual(
            SetValueWindowOptions,
            arguments[..separator],
            "The window scope must remain an option, not become part of the textbox value.");
        CollectionAssert.AreEqual(
            new[] { textBox.Selector, value },
            arguments[(separator + 1)..],
            "The selector and exact value must remain separate literal arguments.");
    }
}
