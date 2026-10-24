// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.CmdPal.UI.ViewModels.UnitTests.ScoringTestCatalog;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>Reports warmed application scoring time and allocations without wall-clock assertions.</summary>
[TestClass]
public sealed partial class ScoringThroughputHarnessTests
{
    private const int CommandCount = 300;
    private const int GlobalFallbackCount = 5;
    private const int PinnedAppCount = 20;
    private const int HistorySeedCount = 200;
    private const int WarmupIterations = 3;
    private const int MeasuredIterations = 10;

    private static readonly string[] Queries = ["c", "ca", "cal", "calc", "vsc", "vs code", "alias42", "Profile3", "Contoso", @"Contoso.App42\app42.exe", "start", "program", "apps"];

    public TestContext TestContext { get; set; } = null!;

    private static ScoringFunction<IListItem> BuildScoringFunction(
        IRecentCommandsManager history,
        IPrecomputedFuzzyMatcher matcher,
        string query)
    {
        var appSearch = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameAndStem);
        var now = DateTimeOffset.UtcNow;
        return (in FuzzyQuery q, IListItem item) => MainListPage.ScoreTopLevelItem(q, item, history, matcher, appSearch, now: now);
    }

    private static (double Milliseconds, long Bytes) Measure(Action action)
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        var stopwatch = new Stopwatch();
        var before = GC.GetTotalAllocatedBytes(precise: true);
        stopwatch.Start();
        for (var i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        stopwatch.Stop();
        return (stopwatch.Elapsed.TotalMilliseconds / MeasuredIterations, (GC.GetTotalAllocatedBytes(precise: true) - before) / MeasuredIterations);
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(3000)]
    public void FullKeystroke_AttributesCostAcrossBuckets(int appCount)
    {
        var apps = BuildAppCatalog(appCount);
        var commands = BuildCatalog(CommandCount, "cmd").Cast<IListItem>().Concat(apps.Take(PinnedAppCount)).ToArray();
        var globalFallbacks = BuildCatalog(GlobalFallbackCount, "gfb").Cast<IListItem>().ToList();
        var pinnedIds = new HashSet<string>(apps.Take(PinnedAppCount).Select(app => app.Command!.Id), StringComparer.Ordinal);
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        history.PrewarmIndex();

        TestContext.WriteLine($"Catalog: {appCount} AppListItems, {CommandCount} commands, {PinnedAppCount} pinned apps. Warmup: {WarmupIterations}; measured: {MeasuredIterations}.");
        TestContext.WriteLine("query | appsEnum ms | cmdScore ms | appScore ms | fallback ms | total ms | total bytes | app matches");
        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);
            var scorer = BuildScoringFunction(history, matcher, raw);
            IListItem[] candidates = [];
            var enumeration = Measure(() => candidates = apps.Where(app => !pinnedIds.Contains(app.Command!.Id)).Cast<IListItem>().ToArray());
            var commandScoring = Measure(() => _ = InternalListHelpers.FilterListWithScores(commands, query, scorer));
            RoScored<IListItem>[] appScored = [];
            var appScoring = Measure(() => appScored = InternalListHelpers.FilterListWithScoresParallel(candidates, query, scorer));
            var fallbackScoring = Measure(() => _ = MainListPage.ScoreDeferredFallbacks(globalFallbacks, query, scorer));
            var totalMilliseconds = enumeration.Milliseconds + commandScoring.Milliseconds + appScoring.Milliseconds + fallbackScoring.Milliseconds;
            var totalBytes = enumeration.Bytes + commandScoring.Bytes + appScoring.Bytes + fallbackScoring.Bytes;
            TestContext.WriteLine($"{raw,-16}| {enumeration.Milliseconds:F3} | {commandScoring.Milliseconds:F3} | {appScoring.Milliseconds:F3} | {fallbackScoring.Milliseconds:F3} | {totalMilliseconds:F3} | {totalBytes} | {appScored.Length}");

            Assert.AreEqual(appCount - PinnedAppCount, candidates.Length);
            if (raw is "alias42" or "Profile3" or "Contoso" or @"Contoso.App42\app42.exe")
            {
                Assert.IsTrue(appScored.Length > 0, $"Metadata query '{raw}' must exercise application matches.");
            }

            var repeated = InternalListHelpers.FilterListWithScoresParallel(candidates, query, scorer);
            CollectionAssert.AreEqual(appScored.Select(result => (result.Item, result.Score)).ToArray(), repeated.Select(result => (result.Item, result.Score)).ToArray());
        }
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(3000)]
    public void AppScoring_SerialAndParallel_ReportThroughputAndAllocations(int appCount)
    {
        var apps = BuildAppCatalog(appCount);
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var source = apps.Cast<IListItem>().ToArray();
        history.PrewarmIndex();

        TestContext.WriteLine($"CPU count: {Environment.ProcessorCount}. Catalog: {appCount} AppListItems.");
        TestContext.WriteLine("query | serial ms | parallel ms | serial bytes | parallel bytes | matches");
        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);
            var scorer = BuildScoringFunction(history, matcher, raw);
            RoScored<IListItem>[] serialResult = [];
            var serial = Measure(() => serialResult = InternalListHelpers.FilterListWithScores(source, query, scorer));
            RoScored<IListItem>[] parallelResult = [];
            var parallel = Measure(() => parallelResult = InternalListHelpers.FilterListWithScoresParallel(source, query, scorer));

            TestContext.WriteLine($"{raw,-16}| {serial.Milliseconds:F3} | {parallel.Milliseconds:F3} | {serial.Bytes} | {parallel.Bytes} | {serialResult.Length}");
            CollectionAssert.AreEqual(serialResult.Select(result => (result.Item, result.Score)).ToArray(), parallelResult.Select(result => (result.Item, result.Score)).ToArray());
        }
    }
}
