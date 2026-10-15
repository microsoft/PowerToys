// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerToys.Telemetry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Telemetry;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class CliTelemetryTests
{
    [DataTestMethod]
    [DataRow(new string[] { }, "none")]
    [DataRow(new[] { "list" }, "list")]
    [DataRow(new[] { "GET", "-i", "private-monitor-id" }, "get")]
    [DataRow(new[] { "set", "-i", "private-monitor-id", "--brightness", "50" }, "set")]
    [DataRow(new[] { "up", "--brightness" }, "up")]
    [DataRow(new[] { "down", "--volume" }, "down")]
    [DataRow(new[] { "capabilities", "-n", "1" }, "capabilities")]
    [DataRow(new[] { "profiles" }, "profiles")]
    [DataRow(new[] { "apply-profile", "42" }, "apply-profile")]
    [DataRow(new[] { "--help" }, "help")]
    [DataRow(new[] { "-h" }, "help")]
    [DataRow(new[] { "-?" }, "help")]
    [DataRow(new[] { "/?" }, "help")]
    [DataRow(new[] { "--version" }, "version")]
    [DataRow(new[] { "get", "--help" }, "get")]
    [DataRow(new[] { "--json", "profiles" }, "unknown")]
    [DataRow(new[] { "@C:\\private\\commands.rsp" }, "unknown")]
    [DataRow(new[] { "unexpected-private-value", "list" }, "unknown")]
    public void CommandName_ContainsOnlyAllowedValues(string[] args, string expected)
    {
        Assert.AreEqual(expected, Program.GetTelemetryCommandName(args));
    }

    [DataTestMethod]
    [DataRow(CliExitCodes.Ok, true)]
    [DataRow(CliExitCodes.ArgumentError, false)]
    [DataRow(CliExitCodes.ProviderUnavailable, false)]
    [DataRow(CliExitCodes.Timeout, false)]
    [DataRow(CliExitCodes.InternalError, false)]
    public async Task Invocation_ReportsFinalResultExactlyOnce(int commandExitCode, bool successful)
    {
        var events = new List<PowerDisplayCLICommandEvent>();
        var runCount = 0;
        var args = new[] { "set", "-n", "1,2", "--brightness", "50" };

        var exitCode = await Program.RunWithTelemetryAsync(
            args,
            receivedArgs =>
            {
                runCount++;
                Assert.AreSame(args, receivedArgs);
                return Task.FromResult(commandExitCode);
            },
            events.Add);

        Assert.AreEqual(commandExitCode, exitCode);
        Assert.AreEqual(1, runCount);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("PowerDisplay_CLICommand", events[0].EventName);
        Assert.AreEqual("set", events[0].CommandName);
        Assert.AreEqual(successful, events[0].Successful);
        Assert.AreEqual(PartA_PrivTags.ProductAndServiceUsage, events[0].PartA_PrivTags);
    }

    [TestMethod]
    public async Task CommandException_IsPreservedAndReportsFailureOnce()
    {
        var events = new List<PowerDisplayCLICommandEvent>();
        var failure = new InvalidOperationException("command failed");

        var actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => Program.RunWithTelemetryAsync(
            ["list"],
            _ => Task.FromException<int>(failure),
            events.Add));

        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, events.Count);
        Assert.IsFalse(events[0].Successful);
    }

    [DataTestMethod]
    [DataRow(CliExitCodes.Ok)]
    [DataRow(CliExitCodes.ArgumentError)]
    public async Task TelemetryException_DoesNotChangeExitCodeOrRetry(int commandExitCode)
    {
        var attempts = 0;

        var exitCode = await Program.RunWithTelemetryAsync(
            ["--help"],
            _ => Task.FromResult(commandExitCode),
            _ =>
            {
                attempts++;
                throw new InvalidOperationException("telemetry unavailable");
            });

        Assert.AreEqual(commandExitCode, exitCode);
        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task TelemetryException_DoesNotReplaceCommandException()
    {
        var failure = new InvalidOperationException("command failed");

        var actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => Program.RunWithTelemetryAsync(
            ["list"],
            _ => Task.FromException<int>(failure),
            _ => throw new ArgumentException("telemetry unavailable")));

        Assert.AreSame(failure, actual);
    }
}
