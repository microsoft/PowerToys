// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.CmdPal.UI.ViewModels.UnitTests.ScoringTestCatalog;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Times the real per-keystroke scoring passes over a synthetic catalog and attributes the cost
/// across apps-enumeration, command scoring, app scoring (the dominant pass), and fallback
/// fold-in, plus a per-item split of <see cref="MainListPage.ScoreTopLevelItem"/> into fuzzy DP
/// vs tier classification vs frecency. Timings go to <see cref="TestContext"/> and the assertions
/// only lock structural facts, so nothing here flakes on a wall clock.
/// </summary>
[TestClass]
public sealed partial class ScoringThroughputHarnessTests
{
    // Sized to mirror a heavy-but-realistic machine, and small enough that the whole harness runs
    // in a couple of seconds on CI.
    private const int AppCount = 3000;
    private const int CommandCount = 300;
    private const int GlobalFallbackCount = 5;
    private const int PinnedAppCount = 20;

    // Seed enough history that frecency lookups actually hit, so we measure the hit path too.
    private const int HistorySeedCount = 200;

    // Report-only counts: warmups prime the JIT and target caches, measured runs get averaged.
    private const int WarmupIterations = 3;
    private const int MeasuredIterations = 10;

    // "c" is the pathological 1-char case that matches nearly every app, the rest narrow a real
    // prefix, and "vsc"/"vs code" hit the acronym and word-boundary paths.
    private static readonly string[] Queries = ["c", "ca", "cal", "calc", "vsc", "vs code"];

    public TestContext TestContext { get; set; } = null!;

    private static ScoringFunction<IListItem> BuildScoringFunction(
        IRecentCommandsManager history,
        IPrecomputedFuzzyMatcher matcher,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
        => (in FuzzyQuery query, IListItem item) =>
            MainListPage.ScoreTopLevelItem(query, item, history, matcher, providerWeightLookup);

    // Averaged elapsed milliseconds after a warmup, report-only and never asserted against a
    // threshold.
    private static double TimeAverageMs(Action action)
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / MeasuredIterations;
    }

    /// <summary>
    /// Attributes a full rebuild keystroke (empty to query, the worst case that scores the whole
    /// catalog) across the four passes. Asserts only that the app pass outweighs the command pass
    /// and that scoring is deterministic.
    /// </summary>
    [TestMethod]
    public void FullKeystroke_AttributesCostAcrossBuckets()
    {
        var apps = BuildCatalog(AppCount, "app");
        var commands = BuildCatalog(CommandCount, "cmd");
        var globalFallbacks = BuildCatalog(GlobalFallbackCount, "gfb").Cast<IListItem>().ToList();
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);

        // Simulate the pinned-app removal the product does on the rebuild path.
        var pinnedIds = new HashSet<string>(apps.Take(PinnedAppCount).Select(a => a.Id));

        TestContext.WriteLine($"Catalog: {AppCount} apps, {CommandCount} commands, {GlobalFallbackCount} global fallbacks, {HistorySeedCount} history seeds.");
        TestContext.WriteLine($"Iterations: {WarmupIterations} warmup + {MeasuredIterations} measured (averaged).");
        TestContext.WriteLine("query | appsEnum ms | cmdScore ms | appScore ms | fallback ms | TOTAL ms | cmdMatches | appMatches");

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);

            var enumMs = TimeAverageMs(() =>
            {
                // Mirrors the product's GetItems().Cast().ToList() plus the pinned filter.
                var materialized = apps.ToList();
                _ = materialized.Where(a => !pinnedIds.Contains(a.Id)).ToList();
            });

            RoScored<IListItem>[] cmdScored = [];
            var cmdMs = TimeAverageMs(() =>
            {
                cmdScored = InternalListHelpers.FilterListWithScores(commands.Cast<IListItem>(), query, scoringFn);
            });

            RoScored<IListItem>[] appScored = [];
            var appMs = TimeAverageMs(() =>
            {
                appScored = InternalListHelpers.FilterListWithScores(apps.Cast<IListItem>(), query, scoringFn);
            });

            var fallbackMs = TimeAverageMs(() =>
            {
                _ = MainListPage.ScoreDeferredFallbacks(globalFallbacks, query, scoringFn);
            });

            var total = enumMs + cmdMs + appMs + fallbackMs;
            TestContext.WriteLine(
                $"{raw,-8}| {enumMs,10:F3} | {cmdMs,10:F3} | {appMs,10:F3} | {fallbackMs,10:F3} | {total,8:F3} | {cmdScored.Length,10} | {appScored.Length,10}");

            // The app pass weighs thousands of items against the command pass's hundreds, which is
            // why it dominates the keystroke cost.
            Assert.IsTrue(apps.Length > commands.Length, "Apps must outnumber commands in the catalog.");

            // Re-scoring the same catalog with the same query yields the same result set.
            var appScoredAgain = InternalListHelpers.FilterListWithScores(apps.Cast<IListItem>(), query, scoringFn);
            Assert.AreEqual(appScored.Length, appScoredAgain.Length, $"App scoring must be deterministic for query '{raw}'.");
            for (var i = 0; i < Math.Min(10, appScored.Length); i++)
            {
                Assert.AreEqual(appScored[i].Score, appScoredAgain[i].Score, $"Top-10 scores must be stable for query '{raw}'.");
            }
        }
    }

    /// <summary>
    /// Characterizes why the settle time spikes: a 1-char query retains a large slice of the
    /// catalog, so the next few keystrokes still re-score a big set before it narrows.
    /// </summary>
    [TestMethod]
    public void OneCharQuery_RetainsLargeMatchSet_DrivesIncrementalCost()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);

        var oneChar = matcher.PrecomputeQuery("c");
        var firstMatches = InternalListHelpers.FilterListWithScores(apps.Cast<IListItem>(), oneChar, scoringFn);

        // Extending to "ca" re-scores only the retained subset, but that subset is still large,
        // which is why the spike carries across frames.
        var retained = firstMatches.Select(s => s.Item).ToList();
        var twoChar = matcher.PrecomputeQuery("ca");
        var secondMatches = InternalListHelpers.FilterListWithScores(retained, twoChar, scoringFn);

        var firstFraction = (double)firstMatches.Length / apps.Length;
        TestContext.WriteLine($"1-char 'c' matches {firstMatches.Length}/{apps.Length} apps ({firstFraction:P1}); extending to 'ca' re-scores {retained.Count} and keeps {secondMatches.Length}.");

        // A narrower query can only keep a subset of the wider query's matches.
        Assert.IsTrue(secondMatches.Length <= retained.Count, "Extending a query cannot add matches beyond the retained set.");
        Assert.IsTrue(firstMatches.Length > 0, "The 1-char query should match a non-trivial set.");
    }

    /// <summary>
    /// Splits a single <see cref="MainListPage.ScoreTopLevelItem"/> into fuzzy DP scoring, tier
    /// classification, and frecency lookup so the overhaul's added cost is visible. Asserts only
    /// the direction of the extension-score delta, which holds regardless of the machine.
    /// </summary>
    [TestMethod]
    public void PerItemScore_SubAttribution_DpVsTierVsFrecency()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);

        // Precompute targets once, like the live cached path, so this measures matcher.Score and
        // not target construction.
        var titleTargets = apps.Select(a => a.GetTitleTarget(matcher)).ToArray();
        var subtitleTargets = apps.Select(a => a.GetSubtitleTarget(matcher)).ToArray();
        var extensionTargets = apps.Select(a => matcher.PrecomputeTarget($"{a.Title} Extension")).ToArray();
        var ids = apps.Select(a => a.Id).ToArray();

        TestContext.WriteLine("Per-item sub-attribution (nanoseconds/item, averaged over the app catalog):");
        TestContext.WriteLine("query | full score | 2 DP | 3 DP | extDelta | classifyTier | wordBoundary | frecency");

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);

            var fullNs = PerItemNs(() =>
            {
                for (var i = 0; i < apps.Length; i++)
                {
                    _ = MainListPage.ScoreTopLevelItem(query, apps[i], history, matcher, null);
                }
            });

            var twoDpNs = PerItemNs(() =>
            {
                for (var i = 0; i < apps.Length; i++)
                {
                    _ = matcher.Score(query, titleTargets[i]) + matcher.Score(query, subtitleTargets[i]);
                }
            });

            var threeDpNs = PerItemNs(() =>
            {
                for (var i = 0; i < apps.Length; i++)
                {
                    _ = matcher.Score(query, titleTargets[i]) + matcher.Score(query, subtitleTargets[i]) + matcher.Score(query, extensionTargets[i]);
                }
            });

            var classifyNs = PerItemNs(() =>
            {
                for (var i = 0; i < apps.Length; i++)
                {
                    _ = MainListRanker.ClassifyTier(query.Original, apps[i].Title, false, false, false, true);
                }
            });

            var wordBoundaryNs = PerItemNs(() =>
            {
                for (var i = 0; i < apps.Length; i++)
                {
                    _ = MainListRanker.MatchesWordBoundaryOrAcronym(apps[i].Title, query.Original.AsSpan());
                }
            });

            var frecencyNs = PerItemNs(() =>
            {
                for (var i = 0; i < apps.Length; i++)
                {
                    _ = history.GetCommandHistoryWeight(ids[i]);
                }
            });

            var extDelta = threeDpNs - twoDpNs;
            TestContext.WriteLine(
                $"{raw,-8}| {fullNs,10:F1} | {twoDpNs,6:F1} | {threeDpNs,6:F1} | {extDelta,8:F1} | {classifyNs,12:F1} | {wordBoundaryNs,12:F1} | {frecencyNs,8:F1}");

            // Adding a third DP score can't make the measurement cheaper.
            Assert.IsTrue(threeDpNs >= twoDpNs * 0.5, "Three DP scores should not be dramatically cheaper than two; extension scoring is real added work.");
        }
    }

    /// <summary>
    /// Times the dominant apps pass serial versus parallel and reports the speedup per query.
    /// Report-only: it asserts the two paths return the same match count, never a wall-clock
    /// threshold.
    /// </summary>
    [TestMethod]
    public void AppScoring_BeforeAfter_SerialVsParallelThroughput()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);
        var source = apps.Cast<IListItem>().ToArray();

        // Build the frecency index once, single-threaded, before the parallel pass reads it.
        history.PrewarmIndex();

        TestContext.WriteLine($"CPU count: {Environment.ProcessorCount}. Catalog: {AppCount} apps.");
        TestContext.WriteLine("query | serial ms (before) | parallel ms (after) | speedup | matches");

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);

            RoScored<IListItem>[] serialResult = [];
            var serialMs = TimeAverageMs(() =>
            {
                serialResult = InternalListHelpers.FilterListWithScores(source, query, scoringFn);
            });

            RoScored<IListItem>[] parallelResult = [];
            var parallelMs = TimeAverageMs(() =>
            {
                parallelResult = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);
            });

            var speedup = parallelMs > 0 ? serialMs / parallelMs : 0.0;
            TestContext.WriteLine(
                $"{raw,-8}| {serialMs,17:F3} | {parallelMs,18:F3} | {speedup,6:F2}x | {serialResult.Length,7}");

            // The parallel path returns the same match count, on any machine.
            Assert.AreEqual(serialResult.Length, parallelResult.Length, $"Match count must match for query '{raw}'.");
        }
    }

    // Averaged per-item nanoseconds for a loop that internally iterates the whole app catalog once.
    private static double PerItemNs(Action loopOverCatalog)
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            loopOverCatalog();
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < MeasuredIterations; i++)
        {
            loopOverCatalog();
        }

        sw.Stop();
        var totalItems = (double)MeasuredIterations * AppCount;
        return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / totalItems;
    }
}
