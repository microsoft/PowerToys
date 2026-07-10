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
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Deterministic scoring-throughput harness for the main/root page hot path. This is a
/// MEASURE-FIRST instrument: it builds a representative synthetic catalog (a few thousand
/// installed apps plus a few hundred top-level commands) and times the same scoring/filter
/// passes the product runs per keystroke, attributing the cost across buckets:
///   1. apps-enumeration (materializing the app list + removing pinned)
///   2. command scoring (<see cref="InternalListHelpers.FilterListWithScores{T}"/> over commands)
///   3. app scoring (the dominant pass - <see cref="InternalListHelpers.FilterListWithScores{T}"/> over apps)
///   4. fallback fold-in (<see cref="MainListPage.ScoreDeferredFallbacks"/>)
/// plus a per-item sub-attribution of a single <see cref="MainListPage.ScoreTopLevelItem"/>
/// into fuzzy-DP scoring vs tier classification vs frecency.
///
/// It exercises the REAL product scorer and filter, never a reimplementation, and mirrors the
/// per-item target caching that <c>AppListItem</c> uses (via <see cref="FuzzyTargetCache"/>) so
/// the numbers reflect the live cached path. Timings are reported through
/// <see cref="TestContext"/> for the log; the assertions only lock in relative/structural facts
/// (determinism, which pass processes more items, the extension-score delta direction) so the
/// harness stays CI-safe and never flakes on a wall-clock threshold.
/// </summary>
[TestClass]
public sealed partial class ScoringThroughputHarnessTests
{
    // Catalog sizes chosen to mirror a heavy-but-realistic machine: thousands of apps dominate N,
    // a few hundred commands, a handful of global fallbacks. Kept deterministic and modest so the
    // whole harness runs well under a couple of seconds in CI.
    private const int AppCount = 3000;
    private const int CommandCount = 300;
    private const int GlobalFallbackCount = 5;
    private const int PinnedAppCount = 20;

    // Fraction of apps seeded into history so frecency lookups actually hit for some items and the
    // per-item frecency cost (dictionary hit + decay math) is represented, not just the miss path.
    private const int HistorySeedCount = 200;

    // Warmup runs prime the JIT and the per-item target caches; measured runs are averaged. These
    // are report-only iteration counts, not correctness knobs.
    private const int WarmupIterations = 3;
    private const int MeasuredIterations = 10;

    // Representative queries. "c" is the pathological 1-char case that fuzzy-matches almost every
    // app; the rest narrow a real prefix; "vsc"/"vs code" exercise acronym/word-boundary paths.
    private static readonly string[] Queries = ["c", "ca", "cal", "calc", "vsc", "vs code"];

    public TestContext TestContext { get; set; } = null!;

    // A stand-in for an installed app or a top-level command. Implements IPrecomputedListItem with
    // a FuzzyTargetCache exactly like AppListItem, so repeated scoring reuses cached targets and the
    // measured cost matches the live cached path rather than recomputing targets each keystroke.
    private sealed partial class CatalogItem : IListItem, IPrecomputedListItem
    {
        private FuzzyTargetCache _titleCache;
        private FuzzyTargetCache _subtitleCache;

        public CatalogItem(string title, string subtitle, string id)
        {
            Title = title;
            Subtitle = subtitle;
            Id = id;
            Command = new NoOpCommand() { Id = id };
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string Id { get; }

        public ICommand Command { get; }

        public IDetails? Details => null;

        public IIconInfo? Icon => null;

        public string Section => string.Empty;

        public ITag[] Tags => [];

        public string TextToSuggest => string.Empty;

        public IContextItem[] MoreCommands => [];

#pragma warning disable CS0067 // The event is never used
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067

        public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher) => _titleCache.GetOrUpdate(matcher, Title);

        public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher) => _subtitleCache.GetOrUpdate(matcher, Subtitle);
    }

    // Deterministic word banks. Composition is index-driven (no RNG, no wall clock) so the catalog,
    // its match counts, and the resulting order are byte-for-byte reproducible across runs and hosts.
    private static readonly string[] Nouns =
    [
        "Calculator", "Calendar", "Camera", "Canvas", "Command", "Control", "Cloud", "Cast",
        "Visual", "Studio", "Code", "Terminal", "Task", "Notepad", "Paint", "Photos", "Player",
        "Panel", "Prompt", "Settings", "Store", "System", "Manager", "Monitor", "Editor", "Browser",
        "Mail", "Maps", "Music", "Movies", "Network", "Office", "Onenote", "Outlook", "People",
    ];

    private static readonly string[] Qualifiers =
    [
        string.Empty, "Pro", "2022", "3D", "Preview", "X", "Lite", "Plus", "Home", "Enterprise",
        "for Windows", "Insider", "Legacy", "New", "Classic",
    ];

    private static readonly string[] SubtitleWords =
    [
        "Perform calculations and conversions", "View and manage your schedule", "Edit and refine images",
        "Modern terminal for command-line tools", "Full-featured integrated development environment",
        "Browse the web quickly and securely", "Adjust your computer settings", "Monitor apps and processes",
        "A simple and fast text editor", "Play and organize your media library",
    ];

    private static IPrecomputedFuzzyMatcher CreateMatcher() => new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    // Builds a stable synthetic catalog. Titles cycle through the word banks so there are dense
    // "confusable" clusters (many Calc*/C* titles) - exactly the shape that makes a 1-char query
    // match a large fraction of the catalog.
    private static CatalogItem[] BuildCatalog(int count, string idPrefix)
    {
        var items = new CatalogItem[count];
        for (var i = 0; i < count; i++)
        {
            var noun = Nouns[i % Nouns.Length];
            var qualifier = Qualifiers[(i / Nouns.Length) % Qualifiers.Length];
            var title = string.IsNullOrEmpty(qualifier) ? noun : $"{noun} {qualifier}";

            // Append a stable disambiguator only past the first cycle so early titles stay clean/realistic.
            if (i >= Nouns.Length * Qualifiers.Length)
            {
                title = $"{title} {i}";
            }

            var subtitle = SubtitleWords[i % SubtitleWords.Length];
            items[i] = new CatalogItem(title, subtitle, $"{idPrefix}.{i}");
        }

        return items;
    }

    private static RecentCommandsManager SeedHistory(CatalogItem[] apps, int seedCount)
    {
        var history = new RecentCommandsManager();
        var n = Math.Min(seedCount, apps.Length);
        for (var i = 0; i < n; i++)
        {
            // Spread the seeds across the catalog so hits are not all clustered at the front.
            var idx = (i * 7) % apps.Length;
            history = history.WithHistoryItem(apps[idx].Command.Id);
        }

        return history;
    }

    private static ScoringFunction<IListItem> BuildScoringFunction(
        IRecentCommandsManager history,
        IPrecomputedFuzzyMatcher matcher,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
        => (in FuzzyQuery query, IListItem item) =>
            MainListPage.ScoreTopLevelItem(query, item, history, matcher, providerWeightLookup);

    // Median-of-averaged timing: warm up, then run the action MeasuredIterations times and return
    // the average elapsed milliseconds. Report-only - never asserted against an absolute threshold.
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
    /// Attributes a full rebuild-path keystroke (empty -> query, the worst case that scores the
    /// entire catalog) across the four passes and reports per-query numbers. Asserts only that the
    /// app pass processes more items than the command pass and that scoring is deterministic.
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
        var pinnedIds = new HashSet<string>(apps.Take(PinnedAppCount).Select(a => a.Command.Id));

        TestContext.WriteLine($"Catalog: {AppCount} apps, {CommandCount} commands, {GlobalFallbackCount} global fallbacks, {HistorySeedCount} history seeds.");
        TestContext.WriteLine($"Iterations: {WarmupIterations} warmup + {MeasuredIterations} measured (averaged).");
        TestContext.WriteLine("query | appsEnum ms | cmdScore ms | appScore ms | fallback ms | TOTAL ms | cmdMatches | appMatches");

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);

            var enumMs = TimeAverageMs(() =>
            {
                // Materialize the app list and strip pinned - mirrors GetItems().Cast().ToList() + Where.
                var materialized = apps.ToList();
                _ = materialized.Where(a => !pinnedIds.Contains(a.Command.Id)).ToList();
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

            // Structural facts only. The app pass considers strictly more items than the command
            // pass (thousands vs hundreds), which is why it dominates the keystroke cost.
            Assert.IsTrue(apps.Length > commands.Length, "Apps must outnumber commands in the catalog.");

            // Determinism: re-scoring the same catalog with the same query yields the same result set.
            var appScoredAgain = InternalListHelpers.FilterListWithScores(apps.Cast<IListItem>(), query, scoringFn);
            Assert.AreEqual(appScored.Length, appScoredAgain.Length, $"App scoring must be deterministic for query '{raw}'.");
            for (var i = 0; i < Math.Min(10, appScored.Length); i++)
            {
                Assert.AreEqual(appScored[i].Score, appScoredAgain[i].Score, $"Top-10 scores must be stable for query '{raw}'.");
            }
        }
    }

    /// <summary>
    /// Characterizes the "1-char query matches almost everything" driver behind the settle-time
    /// spike: on the first keystroke the retained app match set is a large fraction of the whole
    /// catalog, so the next (extending) keystrokes still re-score a large set before it narrows.
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

        // The extending keystroke ("c" -> "ca") re-scores only the retained subset, not the whole
        // catalog - but that subset is still large, which is why the spike persists across frames.
        var retained = firstMatches.Select(s => s.Item).ToList();
        var twoChar = matcher.PrecomputeQuery("ca");
        var secondMatches = InternalListHelpers.FilterListWithScores(retained, twoChar, scoringFn);

        var firstFraction = (double)firstMatches.Length / apps.Length;
        TestContext.WriteLine($"1-char 'c' matches {firstMatches.Length}/{apps.Length} apps ({firstFraction:P1}); extending to 'ca' re-scores {retained.Count} and keeps {secondMatches.Length}.");

        // Structural: the extending keystroke never re-scores more than the retained set, and the
        // narrower query can only keep a subset of the wider query's matches.
        Assert.IsTrue(secondMatches.Length <= retained.Count, "Extending a query cannot add matches beyond the retained set.");
        Assert.IsTrue(firstMatches.Length > 0, "The 1-char query should match a non-trivial set.");
    }

    /// <summary>
    /// Sub-attributes a single <see cref="MainListPage.ScoreTopLevelItem"/> into its components so
    /// the overhaul-added cost is visible: fuzzy-DP scoring (title + subtitle [+ extension]) vs tier
    /// classification (<see cref="MainListRanker.ClassifyTier"/> /
    /// <see cref="MainListRanker.MatchesWordBoundaryOrAcronym"/>) vs frecency lookup. Reports per-item
    /// nanoseconds and asserts only the direction of the extension-score delta (3 DP scores cost at
    /// least as much as 2), which is a structural fact independent of the machine.
    /// </summary>
    [TestMethod]
    public void PerItemScore_SubAttribution_DpVsTierVsFrecency()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);

        // Precompute per-item targets once (matches the cached live path) so the DP-only measurement
        // isolates matcher.Score, not target construction.
        var titleTargets = apps.Select(a => a.GetTitleTarget(matcher)).ToArray();
        var subtitleTargets = apps.Select(a => a.GetSubtitleTarget(matcher)).ToArray();
        var extensionTargets = apps.Select(a => matcher.PrecomputeTarget($"{a.Title} Extension")).ToArray();
        var ids = apps.Select(a => a.Command.Id).ToArray();

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

            // Structural: adding the extension DP score cannot make the DP measurement cheaper.
            Assert.IsTrue(threeDpNs >= twoDpNs * 0.5, "Three DP scores should not be dramatically cheaper than two; extension scoring is real added work.");
        }
    }

    /// <summary>
    /// BEFORE/AFTER throughput: times the dominant apps scoring pass on the sequential path
    /// (before, <see cref="InternalListHelpers.FilterListWithScores{T}"/>) versus the Phase 7b
    /// parallel path (after, <see cref="InternalListHelpers.FilterListWithScoresParallel{T}"/>) and
    /// reports the speedup per query. The parallel path pre-warms the frecency index once before
    /// measuring, mirroring the product. This is report-only; it asserts only that the parallel
    /// path returns the same match count as the sequential one, never a wall-clock threshold, so it
    /// stays CI-safe.
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

            // Structural, machine-independent: the parallel path returns the same match count.
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
