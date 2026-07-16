// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Ipc;
using PowerDisplay.Cli.Options;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Tests for the comma-separated <c>-n</c> batch-monitor feature: option parsing, the per-monitor
/// dispatch/aggregation for the write commands (set/up/down), timeout scaling, and the single-monitor
/// guard for the read commands (get/capabilities).
/// </summary>
[TestClass]
public class BatchMonitorTests
{
    private static ParseResult Parse(params string[] args)
        => new Parser(new PowerDisplayRootCommand()).Parse(args);

    // ── -n parsing ────────────────────────────────────────────────────────────
    [TestMethod]
    public void MonitorNumber_CommaSeparated_ParsesAllIndices()
    {
        var parsed = Parse("set", "-n", "1,2,3", "--brightness", "50");
        var expected = new[] { 1, 2, 3 };
        Assert.AreEqual(0, parsed.Errors.Count);
        CollectionAssert.AreEqual(expected, parsed.GetValueForOption(CliOptions.MonitorNumber));
    }

    [TestMethod]
    public void MonitorNumber_Single_ParsesToOneElement()
    {
        var parsed = Parse("set", "-n", "3", "--brightness", "50");
        var expected = new[] { 3 };
        CollectionAssert.AreEqual(expected, parsed.GetValueForOption(CliOptions.MonitorNumber));
    }

    [TestMethod]
    public void MonitorNumber_Duplicates_CollapsedPreservingFirstSeenOrder()
    {
        var parsed = Parse("up", "--brightness", "-n", "2,1,2");
        var expected = new[] { 2, 1 };
        Assert.AreEqual(0, parsed.Errors.Count);
        CollectionAssert.AreEqual(expected, parsed.GetValueForOption(CliOptions.MonitorNumber));
    }

    [TestMethod]
    public void MonitorNumber_WhitespaceAroundEntries_IsTrimmed()
    {
        var parsed = Parse("set", "-n", " 1 , 2 ", "--brightness", "50");
        var expected = new[] { 1, 2 };
        Assert.AreEqual(0, parsed.Errors.Count);
        CollectionAssert.AreEqual(expected, parsed.GetValueForOption(CliOptions.MonitorNumber));
    }

    [TestMethod]
    public void MonitorNumber_NonInteger_ProducesParseError()
        => Assert.IsTrue(Parse("set", "-n", "1,abc", "--brightness", "50").Errors.Count > 0);

    [TestMethod]
    public void MonitorNumber_TrailingComma_ProducesParseError()
        => Assert.IsTrue(Parse("set", "-n", "1,", "--brightness", "50").Errors.Count > 0);

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("1,0,2")]
    [DataRow("1,-2,3")]
    public void MonitorNumber_NonPositive_ProducesParseError(string monitorNumbers)
        => Assert.IsTrue(Parse("set", "-n", monitorNumbers, "--brightness", "50").Errors.Count > 0);

    [TestMethod]
    public void MonitorNumber_Absent_IsNullOrEmpty()
    {
        var value = Parse("get").GetValueForOption(CliOptions.MonitorNumber);
        Assert.IsTrue(value is null || value.Length == 0);
    }

    // ── target counting + timeout scaling ─────────────────────────────────────
    [TestMethod]
    public void CountDispatchTargets_SetBatch_CountsMonitors()
        => Assert.AreEqual(3, Program.CountDispatchTargets(Parse("set", "-n", "1,2,3", "--brightness", "50")));

    [TestMethod]
    public void CountDispatchTargets_SetSingle_IsOne()
        => Assert.AreEqual(1, Program.CountDispatchTargets(Parse("set", "-n", "2", "--brightness", "50")));

    [TestMethod]
    public void CountDispatchTargets_MonitorIdWins_IsOne()
        => Assert.AreEqual(1, Program.CountDispatchTargets(Parse("set", "-n", "1,2,3", "-i", "MON", "--brightness", "50")));

    [TestMethod]
    public void CountDispatchTargets_NonWriteCommand_IsOne()
        => Assert.AreEqual(1, Program.CountDispatchTargets(Parse("get", "-n", "1")));

    [TestMethod]
    public void ComputeOperationTimeout_ScalesWithTargetCount()
    {
        Assert.AreEqual(Program.OperationTimeout, Program.ComputeOperationTimeout(1));
        Assert.AreEqual(Program.OperationTimeout + Program.PerAdditionalMonitorTimeout, Program.ComputeOperationTimeout(2));
        Assert.AreEqual(Program.OperationTimeout + (Program.PerAdditionalMonitorTimeout * 2), Program.ComputeOperationTimeout(3));

        // A zero/negative target count must never shrink the base deadline.
        Assert.AreEqual(Program.OperationTimeout, Program.ComputeOperationTimeout(0));
    }

    // ── WorseBatchExit aggregation ────────────────────────────────────────────
    [DataTestMethod]
    [DataRow(CliExitCodes.Ok, CliExitCodes.Ok, CliExitCodes.Ok)]
    [DataRow(CliExitCodes.Ok, CliExitCodes.UnsupportedFeature, CliExitCodes.Ok)]
    [DataRow(CliExitCodes.OutOfRange, CliExitCodes.UnsupportedFeature, CliExitCodes.OutOfRange)]
    [DataRow(CliExitCodes.Ok, CliExitCodes.MonitorNotFound, CliExitCodes.MonitorNotFound)]
    [DataRow(CliExitCodes.MonitorNotFound, CliExitCodes.OutOfRange, CliExitCodes.OutOfRange)]
    [DataRow(CliExitCodes.OutOfRange, CliExitCodes.InvalidDiscreteValue, CliExitCodes.InvalidDiscreteValue)]
    [DataRow(CliExitCodes.InvalidDiscreteValue, CliExitCodes.HardwareFailure, CliExitCodes.HardwareFailure)]
    [DataRow(CliExitCodes.HardwareFailure, CliExitCodes.OutOfRange, CliExitCodes.HardwareFailure)]
    public void WorseBatchExit_FoldsToWorstExcludingUnsupported(int current, int next, int expected)
        => Assert.AreEqual(expected, Program.WorseBatchExit(current, next));

    // ── DispatchWriteTargetsAsync routing/aggregation ─────────────────────────
    [TestMethod]
    public async Task DispatchWriteTargets_SingleNumber_DispatchesOnceWithThatNumber()
    {
        var seen = new List<int?>();
        var targets = new[] { 5 };
        var exit = await Program.DispatchWriteTargetsAsync(
            targets,
            monitorId: null,
            number =>
            {
                seen.Add(number);
                return Task.FromResult(CliExitCodes.Ok);
            });

        var expected = new int?[] { 5 };
        CollectionAssert.AreEqual(expected, seen);
        Assert.AreEqual(CliExitCodes.Ok, exit);
    }

    [TestMethod]
    public async Task DispatchWriteTargets_NoSelector_DispatchesOnceWithNull()
    {
        var seen = new List<int?>();
        var exit = await Program.DispatchWriteTargetsAsync(
            Array.Empty<int>(),
            monitorId: null,
            number =>
            {
                seen.Add(number);
                return Task.FromResult(CliExitCodes.SelectorMissing);
            });

        var expected = new int?[] { null };
        CollectionAssert.AreEqual(expected, seen);
        Assert.AreEqual(CliExitCodes.SelectorMissing, exit);
    }

    [TestMethod]
    public async Task DispatchWriteTargets_MonitorIdWins_DispatchesOnceWithNullNumber()
    {
        var seen = new List<int?>();
        var targets = new[] { 1, 2, 3 };
        var exit = await Program.DispatchWriteTargetsAsync(
            targets,
            monitorId: "MON-X",
            number =>
            {
                seen.Add(number);
                return Task.FromResult(CliExitCodes.Ok);
            });

        // A monitor id wins and collapses the batch to a single id-based dispatch (null number).
        var expected = new int?[] { null };
        CollectionAssert.AreEqual(expected, seen);
        Assert.AreEqual(CliExitCodes.Ok, exit);
    }

    [TestMethod]
    public async Task DispatchWriteTargets_Batch_DispatchesEachAndAggregatesWorst()
    {
        var seen = new List<int?>();
        var codes = new Dictionary<int, int>
        {
            [1] = CliExitCodes.Ok,
            [2] = CliExitCodes.OutOfRange,
            [3] = CliExitCodes.HardwareFailure,
        };

        var targets = new[] { 1, 2, 3 };
        var exit = await Program.DispatchWriteTargetsAsync(
            targets,
            monitorId: null,
            number =>
            {
                seen.Add(number);
                return Task.FromResult(codes[number!.Value]);
            });

        var expected = new int?[] { 1, 2, 3 };
        CollectionAssert.AreEqual(expected, seen);
        Assert.AreEqual(CliExitCodes.HardwareFailure, exit);
    }

    [TestMethod]
    public async Task DispatchWriteTargets_Batch_UnsupportedDoesNotFailTheBatch()
    {
        var codes = new Dictionary<int, int>
        {
            [1] = CliExitCodes.Ok,
            [2] = CliExitCodes.UnsupportedFeature,
        };

        var targets = new[] { 1, 2 };
        var exit = await Program.DispatchWriteTargetsAsync(
            targets,
            monitorId: null,
            number => Task.FromResult(codes[number!.Value]));

        Assert.AreEqual(CliExitCodes.Ok, exit);
    }

    [TestMethod]
    public async Task DispatchWriteTargets_Batch_AbortsOnProviderUnavailable()
    {
        var seen = new List<int?>();
        var targets = new[] { 1, 2, 3 };
        var exit = await Program.DispatchWriteTargetsAsync(
            targets,
            monitorId: null,
            number =>
            {
                seen.Add(number);
                return Task.FromResult(number == 1 ? CliExitCodes.ProviderUnavailable : CliExitCodes.Ok);
            });

        var expected = new int?[] { 1 };
        Assert.AreEqual(CliExitCodes.ProviderUnavailable, exit);
        CollectionAssert.AreEqual(expected, seen, "must abort after the first PROVIDER_UNAVAILABLE");
    }

    // ── WarnIfMonitorNumberIgnored: complete ignored list, formatted invariantly ──────────────
    [TestMethod]
    public void WarnIfMonitorNumberIgnored_MultipleNumbers_WarningIncludesCompleteList()
    {
        var output = new RecordingCliOutput();
        int[] monitorNumbers = { 1, 2, 3 };
        Program.WarnIfMonitorNumberIgnored(output, monitorNumbers, "MON-X");

        Assert.AreEqual(1, output.StderrLines.Count);
        StringAssert.Contains(output.StderrLines[0], "1,2,3");

        // Regression guard: the old single-number message must not appear on its own.
        Assert.IsFalse(output.StderrLines[0].EndsWith(" 1 ignored because --monitor-id was also provided", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WarnIfMonitorNumberIgnored_NoMonitorId_NoWarningEmitted()
    {
        var output = new RecordingCliOutput();
        int[] monitorNumbers = { 1, 2, 3 };
        Program.WarnIfMonitorNumberIgnored(output, monitorNumbers, null);
        Assert.AreEqual(0, output.StderrLines.Count);
    }

    [TestMethod]
    public void WarnIfMonitorNumberIgnored_EmptyList_NoWarningEmitted()
    {
        var output = new RecordingCliOutput();
        Program.WarnIfMonitorNumberIgnored(output, Array.Empty<int>(), "MON-X");
        Assert.AreEqual(0, output.StderrLines.Count);
    }

    [TestMethod]
    public async Task Set_MonitorIdWinsOverBatch_WarningIncludesAllIgnoredNumbers()
    {
        var root = new PowerDisplayRootCommand();
        var args = new[] { "set", "-n", "1,2,3", "-i", "MON-X", "--brightness", "50" };
        var parseResult = new Parser(root).Parse(args);
        var output = new RecordingCliOutput();

        // The send delegate is irrelevant here (a null response renders PROVIDER_UNAVAILABLE); the
        // warning is emitted synchronously before the dispatch itself.
        var dispatcher = new IpcDispatcher((_, _, _) => Task.FromResult<string?>(null), output, TimeSpan.FromSeconds(1));

        await Program.DispatchAsync(root, args, parseResult, dispatcher, output, CancellationToken.None);

        var warnings = output.StderrLines.Where(l => l.StartsWith("warn:", StringComparison.Ordinal)).ToList();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "1,2,3");
    }

    // ── get/capabilities reject a comma-separated batch ───────────────────────
    [TestMethod]
    public Task Get_MultipleMonitors_RejectedAsArgumentError()
        => AssertReadCommandRejectsBatch("get");

    [TestMethod]
    public Task Capabilities_MultipleMonitors_RejectedAsArgumentError()
        => AssertReadCommandRejectsBatch("capabilities");

    private static async Task AssertReadCommandRejectsBatch(string command)
    {
        var root = new PowerDisplayRootCommand();
        var args = new[] { command, "-n", "1,2" };
        var parseResult = new Parser(root).Parse(args);
        var output = new RecordingCliOutput();

        // The dispatcher must never be reached: the batch is rejected CLI-side before any IPC.
        var dispatcher = new IpcDispatcher(
            (_, _, _) => throw new InvalidOperationException("dispatcher must not be called for a rejected batch"),
            output,
            TimeSpan.FromSeconds(1));

        var exit = await Program.DispatchAsync(root, args, parseResult, dispatcher, output, CancellationToken.None);

        Assert.AreEqual(CliExitCodes.ArgumentError, exit);
        Assert.AreEqual(1, output.ErrorCount);
        Assert.AreEqual(CliErrorCodes.ArgumentError, output.LastError!.Error.Code);
    }
}
