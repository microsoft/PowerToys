// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using LightSwitch.Cli.Telemetry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LightSwitch.Cli.UnitTests;

[TestClass]
public sealed class ProgramTelemetryTests
{
    private static readonly string[] StatusArguments = { "status", "--json" };

    [TestMethod]
    [DataRow(new string[] { "status", "--json" }, "status")]
    [DataRow(new string[] { "light" }, "light")]
    [DataRow(new string[] { "dark" }, "dark")]
    [DataRow(new string[] { "toggle" }, "toggle")]
    [DataRow(new string[] { "TOGGLE" }, "toggle")]
    [DataRow(new string[] { "schedule", "enable", "--mode", "private-value" }, "schedule")]
    [DataRow(new string[] { "--help" }, "help")]
    [DataRow(new string[] { "-h" }, "help")]
    [DataRow(new string[] { "-?" }, "help")]
    [DataRow(new string[] { "--version" }, "version")]
    [DataRow(new string[] { "--json", "status" }, "unknown")]
    [DataRow(new string[] { "@C:\\private\\arguments.rsp" }, "unknown")]
    [DataRow(new string[] { "private-value", "toggle" }, "unknown")]
    [DataRow(new string[] { }, "none")]
    public void TelemetryCommandNameDoesNotIncludeArguments(string[] args, string expected)
    {
        Assert.AreEqual(expected, Program.GetTelemetryCommandName(args));
    }

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(2, false)]
    [DataRow(6, false)]
    public async Task InvocationRecordsOneEventWithFinalResult(int exitCode, bool successful)
    {
        int eventCount = 0;
        LightSwitchCLICommandEvent? recordedEvent = null;

        int actual = await Program.RunWithTelemetryAsync(
            StatusArguments,
            _ => Task.FromResult(exitCode),
            telemetryEvent =>
            {
                eventCount++;
                recordedEvent = telemetryEvent;
            });

        Assert.AreEqual(exitCode, actual);
        Assert.AreEqual(1, eventCount);
        Assert.IsNotNull(recordedEvent);
        Assert.AreEqual("LightSwitch_CLICommand", recordedEvent.EventName);
        Assert.AreEqual("status", recordedEvent.CommandName);
        Assert.AreEqual(successful, recordedEvent.Successful);
    }

    [TestMethod]
    public async Task UnexpectedFailureRecordsOneFailedEventAndPreservesException()
    {
        var failure = new InvalidOperationException("Simulated command failure.");
        int eventCount = 0;
        bool? recordedSuccess = null;

        var actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => Program.RunWithTelemetryAsync(
            StatusArguments,
            _ => Task.FromException<int>(failure),
            telemetryEvent =>
            {
                eventCount++;
                recordedSuccess = telemetryEvent.Successful;
            }));

        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, eventCount);
        Assert.IsFalse(recordedSuccess);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public async Task TelemetryFailurePreservesExitCode(int exitCode)
    {
        int actual = await Program.RunWithTelemetryAsync(
            StatusArguments,
            _ => Task.FromResult(exitCode),
            _ => throw new InvalidOperationException("Simulated telemetry failure."));

        Assert.AreEqual(exitCode, actual);
    }

    [TestMethod]
    public async Task TelemetryFailurePreservesCommandException()
    {
        var failure = new InvalidOperationException("Simulated command failure.");

        var actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => Program.RunWithTelemetryAsync(
            StatusArguments,
            _ => Task.FromException<int>(failure),
            _ => throw new ArgumentException("Simulated telemetry failure.")));

        Assert.AreSame(failure, actual);
    }
}
