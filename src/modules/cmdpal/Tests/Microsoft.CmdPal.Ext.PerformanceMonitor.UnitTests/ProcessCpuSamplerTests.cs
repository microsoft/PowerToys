// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class ProcessCpuSamplerTests
{
    [TestMethod]
    public void ProcessesWithTheSameNameAreCombined()
    {
        // Two processors for one second: 20,000,000 ticks of capacity.
        var ranked = ProcessCpuSampler.Rank(
            [("chrome.exe", 2_000_000), ("Chrome.exe", 1_000_000), ("devenv.exe", 4_000_000), ("System", 200_000)],
            elapsedTicks: 10_000_000,
            processorCount: 2,
            count: 5);

        Assert.HasCount(3, ranked);
        Assert.AreEqual("devenv", ranked[0].Name);
        Assert.AreEqual(20f, ranked[0].Percent, 1e-4);
        Assert.IsTrue(string.Equals("chrome", ranked[1].Name, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(15f, ranked[1].Percent, 1e-4);
        Assert.AreEqual("System", ranked[2].Name);
    }

    [TestMethod]
    public void OnlyTheBusiestAreReturned()
    {
        var ranked = ProcessCpuSampler.Rank([("a", 3), ("b", 2), ("c", 1)], elapsedTicks: 10, processorCount: 1, count: 2);

        Assert.HasCount(2, ranked);
        Assert.AreEqual("a", ranked[0].Name);
        Assert.AreEqual("b", ranked[1].Name);
    }

    [TestMethod]
    public void NoElapsedTimeMeansNoProcesses()
    {
        Assert.IsEmpty(ProcessCpuSampler.Rank([("a", 3)], elapsedTicks: 0, processorCount: 4, count: 5));
    }

    [TestMethod]
    public void SnapshotsOfThisMachineFindBusyProcesses()
    {
        var sampler = new ProcessCpuSampler();

        Assert.IsEmpty(sampler.Sample(5));

        // Keep this process busy so the second snapshot has something to report.
        var deadline = DateTime.UtcNow.AddMilliseconds(200);
        while (DateTime.UtcNow < deadline)
        {
            Thread.SpinWait(1000);
        }

        var processes = sampler.Sample(5);
        Assert.IsNotEmpty(processes);
        Assert.IsGreaterThan(0, processes[0].Percent);
    }

    [TestMethod]
    public void TopProcessRowsHaveTheNameAndPercent()
    {
        var rows = SystemCPUUsageWidgetPage.CreateTopProcesses([new("devenv", 12.34f)]);

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("devenv", (string)rows[0]["name"]);
        Assert.AreEqual(12.3d, (double)rows[0]["percent"], 1e-9);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, "{0:0.#}%", 12.3), (string)rows[0]["text"]);
    }

    [TestMethod]
    public void HistoryStartsAtTheNewestSnapshotAWindowOld()
    {
        var now = Stopwatch.GetTimestamp();
        List<ProcessCpuSampler.Snapshot> history = [SnapshotFrom(now, 4.5), SnapshotFrom(now, 3.5), SnapshotFrom(now, 2.5), SnapshotFrom(now, 1.5)];

        ProcessCpuSampler.PruneHistory(history, now);

        Assert.HasCount(3, history);
        Assert.AreEqual(SnapshotFrom(now, 3.5).Timestamp, history[0].Timestamp);
    }

    [TestMethod]
    public void HistoryShorterThanAWindowIsKept()
    {
        var now = Stopwatch.GetTimestamp();
        List<ProcessCpuSampler.Snapshot> history = [SnapshotFrom(now, 2), SnapshotFrom(now, 1)];

        ProcessCpuSampler.PruneHistory(history, now);

        Assert.HasCount(2, history);
    }

    [TestMethod]
    public void HistoryFromBeforeAPauseIsDropped()
    {
        var now = Stopwatch.GetTimestamp();
        List<ProcessCpuSampler.Snapshot> history = [SnapshotFrom(now, 600), SnapshotFrom(now, 599)];

        ProcessCpuSampler.PruneHistory(history, now);

        Assert.IsEmpty(history);
    }

    [TestMethod]
    public void ResetForgetsEarlierSnapshots()
    {
        var sampler = new ProcessCpuSampler();
        sampler.Sample(5);

        sampler.Reset();

        Assert.IsEmpty(sampler.Sample(5));
    }

    private static ProcessCpuSampler.Snapshot SnapshotFrom(long now, double secondsAgo) =>
        new(now - (long)(secondsAgo * Stopwatch.Frequency), []);
}
