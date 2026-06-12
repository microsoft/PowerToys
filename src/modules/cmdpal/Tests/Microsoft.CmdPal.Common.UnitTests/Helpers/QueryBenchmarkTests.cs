// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class QueryBenchmarkTests
{
    [TestInitialize]
    public void Setup()
    {
        QueryBenchmark.Instance.IsEnabled = true;
        QueryBenchmark.Instance.Clear();
    }

    [TestCleanup]
    public void Cleanup()
    {
        QueryBenchmark.Instance.IsEnabled = false;
        QueryBenchmark.Instance.Clear();
    }

    // ========================================================================
    // Scenario A — Cold Start Query: Verify session records total time and first result
    // ========================================================================
    [TestMethod]
    public void ScenarioA_ColdStartQuery_RecordsTotalTime()
    {
        var session = QueryBenchmark.Instance.StartSession("notepad", "ColdStart");

        // Simulate extension work
        Thread.SpinWait(1000);

        session.MarkFirstResult();
        session.RecordMilestone("GetItems");

        Thread.SpinWait(1000);

        session.RecordMilestone("FilterComplete");
        session.Complete();

        Assert.IsTrue(session.TotalElapsedMs > 0, "Total elapsed should be positive");
        Assert.IsTrue(session.TimeToFirstResultMs > 0, "First result time should be positive");
        Assert.IsTrue(session.TimeToFirstResultMs <= session.TotalElapsedMs, "First result should be before total");
        Assert.AreEqual(2, session.Milestones.Count, "Should have 2 milestones");
    }

    [TestMethod]
    public void ScenarioA_ColdStartQuery_FirstResultOnlyRecordedOnce()
    {
        var session = QueryBenchmark.Instance.StartSession("settings", "ColdStart");

        session.MarkFirstResult();
        var firstTime = session.TimeToFirstResultMs;

        Thread.SpinWait(10000);

        // Second call should be a no-op
        session.MarkFirstResult();

        Assert.AreEqual(firstTime, session.TimeToFirstResultMs, "Second MarkFirstResult should be ignored");
        session.Complete();
    }

    // ========================================================================
    // Scenario B — Warm Query: Verify multiple sessions are recorded separately
    // ========================================================================
    [TestMethod]
    public void ScenarioB_WarmQuery_MultipleSessionsRecorded()
    {
        // First query
        var session1 = QueryBenchmark.Instance.StartSession("notepad", "WarmQuery");
        session1.MarkFirstResult();
        session1.Complete();

        // Same query again (warm)
        var session2 = QueryBenchmark.Instance.StartSession("notepad", "WarmQuery");
        session2.MarkFirstResult();
        session2.Complete();

        var sessions = QueryBenchmark.Instance.GetCompletedSessions();
        Assert.AreEqual(2, sessions.Length);
        Assert.IsTrue(sessions.All(s => s.Query == "notepad"));
        Assert.IsTrue(sessions.All(s => s.Scenario == "WarmQuery"));
    }

    [TestMethod]
    public void ScenarioB_WarmQuery_ReportShowsAggregates()
    {
        for (var i = 0; i < 5; i++)
        {
            var session = QueryBenchmark.Instance.StartSession("notepad", "WarmQuery");
            Thread.SpinWait(1000);
            session.MarkFirstResult();
            session.Complete();
        }

        var report = QueryBenchmark.Instance.GenerateReport();
        Assert.IsTrue(report.Contains("WarmQuery"));
        Assert.IsTrue(report.Contains("5 samples"));
        Assert.IsTrue(report.Contains("avg:"));
        Assert.IsTrue(report.Contains("p95:"));
    }

    // ========================================================================
    // Scenario C — Complex Query: Verify per-extension timing
    // ========================================================================
    [TestMethod]
    public void ScenarioC_ComplexQuery_RecordsPerExtensionTimings()
    {
        var session = QueryBenchmark.Instance.StartSession("note", "ComplexQuery");

        // Simulate multiple extensions
        session.RecordExtensionTime("Apps", 15.2);
        session.RecordExtensionTime("WebSearch", 120.5);
        session.RecordExtensionTime("Calculator", 2.1);
        session.RecordExtensionTime("Indexer", 45.0);

        session.MarkFirstResult();
        session.Complete();

        Assert.AreEqual(4, session.ExtensionTimings.Count);
        Assert.AreEqual(120.5, session.ExtensionTimings["WebSearch"]);
        Assert.AreEqual(2.1, session.ExtensionTimings["Calculator"]);
    }

    [TestMethod]
    public void ScenarioC_ComplexQuery_ExtensionTimerDisposablePattern()
    {
        var session = QueryBenchmark.Instance.StartSession("note", "ComplexQuery");

        using (session.TimeExtension("SlowExtension"))
        {
            Thread.SpinWait(50000);
        }

        session.Complete();

        Assert.IsTrue(session.ExtensionTimings.ContainsKey("SlowExtension"));
        Assert.IsTrue(session.ExtensionTimings["SlowExtension"] > 0);
    }

    // ========================================================================
    // Scenario D — Rapid Typing: Verify debounce behavior (only last session matters)
    // ========================================================================
    [TestMethod]
    public void ScenarioD_RapidTyping_SessionsForEachKeystroke()
    {
        var queries = new[] { "n", "no", "not", "note" };

        foreach (var query in queries)
        {
            var session = QueryBenchmark.Instance.StartSession(query, "RapidTyping");
            session.MarkFirstResult();
            session.Complete();
        }

        var sessions = QueryBenchmark.Instance.GetCompletedSessions();
        Assert.AreEqual(4, sessions.Length);

        // Verify the queries are in order
        for (var i = 0; i < queries.Length; i++)
        {
            Assert.AreEqual(queries[i], sessions[i].Query);
        }
    }

    // ========================================================================
    // Infrastructure Tests
    // ========================================================================
    [TestMethod]
    public void WhenDisabled_ReturnsNoOpSession()
    {
        QueryBenchmark.Instance.IsEnabled = false;

        var session = QueryBenchmark.Instance.StartSession("test", "Disabled");

        // No-op session should not crash
        session.MarkFirstResult();
        session.RecordMilestone("test");
        session.RecordExtensionTime("ext", 5.0);
        session.Complete();

        Assert.AreEqual(-1, session.TotalElapsedMs);
        Assert.AreEqual(-1, session.TimeToFirstResultMs);
        Assert.AreEqual(0, QueryBenchmark.Instance.GetCompletedSessions().Length);
    }

    [TestMethod]
    public void SessionTrimming_DoesNotExceedMaxRetained()
    {
        // Fill beyond the max
        for (var i = 0; i < 100; i++)
        {
            var session = QueryBenchmark.Instance.StartSession($"query{i}", "TrimTest");
            session.Complete();
        }

        var sessions = QueryBenchmark.Instance.GetCompletedSessions();
        Assert.IsTrue(sessions.Length <= 64, $"Expected <= 64 sessions, got {sessions.Length}");
    }

    [TestMethod]
    public void GenerateReport_EmptyWhenNoSessions()
    {
        var report = QueryBenchmark.Instance.GenerateReport();
        Assert.IsTrue(report.Contains("No sessions recorded"));
    }

    [TestMethod]
    public void Milestones_AreOrderedByTime()
    {
        var session = QueryBenchmark.Instance.StartSession("test", "MilestoneOrder");

        session.RecordMilestone("First");
        Thread.SpinWait(5000);
        session.RecordMilestone("Second");
        Thread.SpinWait(5000);
        session.RecordMilestone("Third");

        session.Complete();

        var milestones = session.Milestones.ToList();
        Assert.AreEqual(3, milestones.Count);
        Assert.IsTrue(milestones[0].ElapsedMs <= milestones[1].ElapsedMs);
        Assert.IsTrue(milestones[1].ElapsedMs <= milestones[2].ElapsedMs);
    }

    [TestMethod]
    public void ReportString_ContainsAllData()
    {
        var session = QueryBenchmark.Instance.StartSession("calc 2+2", "Integration");
        session.RecordMilestone("PreFilter");
        session.RecordExtensionTime("Calculator", 3.5);
        session.MarkFirstResult();
        session.RecordMilestone("FilterComplete");
        session.Complete();

        var report = session.ToReportString();
        Assert.IsTrue(report.Contains("calc 2+2"));
        Assert.IsTrue(report.Contains("Integration"));
        Assert.IsTrue(report.Contains("Calculator"));
        Assert.IsTrue(report.Contains("PreFilter"));
        Assert.IsTrue(report.Contains("FilterComplete"));
    }
}
