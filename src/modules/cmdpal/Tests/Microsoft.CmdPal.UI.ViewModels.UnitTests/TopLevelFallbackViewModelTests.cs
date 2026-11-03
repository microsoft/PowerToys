// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class TopLevelFallbackViewModelTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    // View models hold the page context weakly, so keep it alive for every test.
    private static readonly TestPageContext PageContext = new();

    // Scores a blank title zero, like MainListPage.ScoreTopLevelItem.
    private static readonly Func<FuzzyQuery, InProcObject<IListItem>, string, string, int> TitleScorer =
        (_, _, title, _) => string.IsNullOrWhiteSpace(title) ? 0 : 1;

    [TestMethod]
    [DataRow("Search a", "Search ab", false, false)]
    [DataRow("", "Search a", false, true)]
    [DataRow("Search a", "", false, true)]
    [DataRow("Search a", "  ", false, true)]
    [DataRow("Search a", "Search a", false, false)]
    [DataRow("Search a", "Search ab", true, true)]
    [DataRow("", "Search a", true, true)]
    [DataRow("Search a", "Search a", true, false)]
    public void UpdateFallbackText_ReportsTextChangesOnlyForGlobalFallbacks(string renderedTitle, string newTitle, bool reportRankChanges, bool expected)
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous(renderedTitle, reportRankChanges, CancellationToken.None);
            _ = topLevel.ReadTextForRender();

            Assert.AreEqual(expected, topLevel.SafeUpdateFallbackTextSynchronous(newTitle, reportRankChanges, CancellationToken.None));
            Assert.AreEqual(newTitle, topLevel.Title);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void UpdateFallbackText_LeavesFirstRenderToPendingRefresh()
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);

        try
        {
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: true, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    [DataRow(false, "Search a", "Search ab")]
    [DataRow(true, "Reload", "Reload")]
    public void UpdateFallbackText_ReportsFallbackShownByCancelledUpdate(bool reportRankChanges, string cancelledTitle, string latestTitle)
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = CreateCancelledTokenSource();

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportRankChanges, CancellationToken.None);
            _ = topLevel.ReadTextForRender();

            // The superseded query shows the fallback, but its refresh is dropped.
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous(cancelledTitle, reportRankChanges, superseded.Token));
            Assert.AreEqual(cancelledTitle, topLevel.Title);

            // The latest query must still refresh, or the list never shows the fallback.
            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous(latestTitle, reportRankChanges, CancellationToken.None));

            _ = topLevel.ReadTextForRender();
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous(latestTitle, reportRankChanges, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UpdateFallbackText_ReportsFallbackHiddenByCancelledUpdate(bool reportRankChanges)
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = CreateCancelledTokenSource();

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges, CancellationToken.None);
            _ = topLevel.ReadTextForRender();

            // The superseded query clears the title, and the newer query's first render drops the row.
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportRankChanges, superseded.Token));
            _ = topLevel.ReadTextForRender();

            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Search ab", reportRankChanges, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void UpdateFallbackText_ReportsFallbackHiddenWhileLatestUpdateIsInFlight()
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = CreateCancelledTokenSource();

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: false, CancellationToken.None);
            _ = topLevel.ReadTextForRender();

            // Overlapping calls: the title is visible before and after the latest call, but a
            // superseded call clears it in between and a render drops the row meanwhile.
            fallback.DuringQuery = () =>
            {
                fallback.DuringQuery = null;
                topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportRankChanges: false, superseded.Token);
                _ = topLevel.ReadTextForRender();
            };

            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Search ab", reportRankChanges: false, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void UpdateFallbackText_ReportsChangeWhenADiscardedRenderSawIt()
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);

        try
        {
            // The fetch the list publishes renders before the fallback resolves its title.
            _ = topLevel.ReadTextForRender();

            // An older, discarded fetch renders after the title resolves but before the update
            // compares, so it records a title the published list never showed.
            Action? discardedRender = () => _ = topLevel.ReadTextForRender();
            fallback.Queried += () =>
            {
                discardedRender?.Invoke();
                discardedRender = null;
            };

            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: false, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void UpdateFallbackText_ReportsSubtitleChangeForGlobalFallback()
    {
        var fallback = new QueryTitleFallback("test.calculator") { TitleFor = _ => "3", SubtitleFor = query => query };
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("1+2", reportRankChanges: true, CancellationToken.None);
            _ = topLevel.ReadTextForRender();

            // The next query keeps the title but changes the subtitle after it, and a render lands
            // between the two assignments, so it ranks the old subtitle.
            Action? render = () => _ = topLevel.ReadTextForRender();
            fallback.Queried += () =>
            {
                render?.Invoke();
                render = null;
            };

            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("2+1", reportRankChanges: true, CancellationToken.None));
            Assert.AreEqual("3", topLevel.Title);
            Assert.AreEqual("2+1", topLevel.Subtitle);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UpdateFallbackText_IgnoresSubtitleChangeWhileHidden(bool reportRankChanges)
    {
        // Like Calculator: no title, a placeholder subtitle, then a cleared subtitle once it rejects the query.
        var fallback = new QueryTitleFallback("test.calculator") { TitleFor = _ => string.Empty, SubtitleFor = query => query };
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("Type an expression", reportRankChanges, CancellationToken.None);
            _ = topLevel.ReadTextForRender();

            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportRankChanges, CancellationToken.None));
            Assert.AreEqual(string.Empty, topLevel.Subtitle);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void SelectVisibleFallbacks_RecordsTheTitleItFiltersOn()
    {
        var fallback = new QueryTitleFallback("test.common");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = CreateCancelledTokenSource();
        RoScored<InProcObject<IListItem>>[] fallbackItems = [new(new InProcObject<IListItem>(topLevel), 1)];

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: false, CancellationToken.None);
            Assert.AreEqual(1, MainListPage.SelectVisibleFallbacks(fallbackItems)!.Count);

            // A superseded update clears the title, so this render hides the fallback.
            topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportRankChanges: false, superseded.Token);
            Assert.AreEqual(0, MainListPage.SelectVisibleFallbacks(fallbackItems)!.Count);

            // The latest update shows it again, so it must refresh the list.
            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Search ab", reportRankChanges: false, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void ScoreDeferredFallbacks_RecordsTheTitleItFiltersOn()
    {
        var fallback = new QueryTitleFallback("test.global");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = CreateCancelledTokenSource();
        InProcObject<IListItem>[] sources = [new(topLevel)];
        var scorer = TitleScorer;

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("Reload", reportRankChanges: true, CancellationToken.None);
            Assert.AreEqual(1, MainListPage.ScoreDeferredFallbacks(sources, default, scorer)!.Count);

            // A superseded update clears the title, so this render hides the fallback.
            topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportRankChanges: true, superseded.Token);
            Assert.IsNull(MainListPage.ScoreDeferredFallbacks(sources, default, scorer));

            // The latest update restores the same title, which the list no longer shows.
            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Reload", reportRankChanges: true, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void ScoreDeferredFallbacks_ScoresTheTitleItRecords()
    {
        var fallback = new QueryTitleFallback("test.global");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = CreateCancelledTokenSource();
        InProcObject<IListItem>[] sources = [new(topLevel)];
        var matcher = new PrecomputedFuzzyMatcher();
        var history = new RecentCommandsManager();
        var query = matcher.PrecomputeQuery("reload");
        Func<FuzzyQuery, InProcObject<IListItem>, string, string, int> scorer = (q, item, title, subtitle) =>
        {
            // A superseded update changes the live title to one that doesn't match while this render is scoring.
            topLevel.SafeUpdateFallbackTextSynchronous("Search b", reportRankChanges: true, superseded.Token);
            return MainListPage.ScoreTopLevelItem(in q, item, history, matcher, title: title, subtitle: subtitle);
        };

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous("Reload", reportRankChanges: true, CancellationToken.None);
            var scored = MainListPage.ScoreDeferredFallbacks(sources, query, scorer)!;
            Assert.AreEqual(1, scored.Count);

            // A non-matching fallback still scores at the fallback floor, so the captured match must beat it.
            var liveScore = MainListPage.ScoreTopLevelItem(in query, sources[0], history, matcher);
            Assert.AreEqual("Search b", topLevel.Title);
            Assert.IsTrue(scored[0].Score > liveScore, $"The captured title scored {scored[0].Score}, the live title {liveScore}.");

            // The list ranked "Reload", so the latest update's "Search b" must re-rank it.
            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Search b", reportRankChanges: true, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void ScoreDeferredFallbacks_RecordsBlankGlobalFallbackForItsFirstUpdate()
    {
        var fallback = new QueryTitleFallback("test.global");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        InProcObject<IListItem>[] sources = [new(topLevel)];

        try
        {
            // First paint happens before the fallback resolves a title, so scoring drops it.
            Assert.IsNull(MainListPage.ScoreDeferredFallbacks(sources, default, TitleScorer));

            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: true, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void UpdateFallbackText_SkipsDisabledFallback()
    {
        var fallback = new QueryTitleFallback("test.fallback");
        var settings = new ProviderSettings
        {
            FallbackCommands = ImmutableDictionary<string, FallbackSettings>.Empty
                .Add(fallback.Id, new FallbackSettings(isEnabled: false, includeInGlobalResults: true)),
        };
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, settings, services);

        try
        {
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: true, CancellationToken.None));
            Assert.AreEqual(0, fallback.QueryCount);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void BeginUpdate_RefreshesOnlyForGlobalTitleChanges()
    {
        var global = new QueryTitleFallback("test.global");
        var common = new QueryTitleFallback("test.common");
        using var services = CreateServices();
        var globalTopLevel = CreateTopLevel(global, new ProviderSettings(), services);
        var commonTopLevel = CreateTopLevel(common, new ProviderSettings(), services);
        var refreshes = 0;
        var manager = new FallbackUpdateManager(() => Interlocked.Increment(ref refreshes));

        try
        {
            // Both fallbacks are already listed with a title, so the next query only changes their text.
            globalTopLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: true, CancellationToken.None);
            commonTopLevel.SafeUpdateFallbackTextSynchronous("Search a", reportRankChanges: true, CancellationToken.None);
            _ = globalTopLevel.ReadTextForRender();
            _ = commonTopLevel.ReadTextForRender();
            using var queried = new CountdownEvent(2);
            global.Queried += () => queried.Signal();
            common.Queried += () => queried.Signal();

            manager.BeginUpdate("Search ab", [globalTopLevel, commonTopLevel], globalFallbackCount: 1, CancellationToken.None);

            Assert.IsTrue(queried.Wait(TestTimeout), "Both fallbacks should receive the query.");

            // Disposing drains the workers, so every refresh they would request has been requested.
            manager.Dispose();
            Assert.AreEqual(1, Volatile.Read(ref refreshes), "Only the global fallback's text change should refresh the list.");
            Assert.AreEqual("Search ab", commonTopLevel.Title);
        }
        finally
        {
            manager.Dispose();
            globalTopLevel.Cleanup();
            commonTopLevel.Cleanup();
        }
    }

    private static CancellationTokenSource CreateCancelledTokenSource()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source;
    }

    private static ServiceProvider CreateServices() => new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();

    private static TopLevelFallbackViewModel CreateTopLevel(QueryTitleFallback fallback, ProviderSettings settings, IServiceProvider services)
    {
        var source = new CommandItemViewModel(new(fallback), new(PageContext), DefaultContextMenuFactory.Instance);
        source.InitializeProperties();
        return new TopLevelFallbackViewModel(source, CommandPaletteHost.Instance, CommandProviderContext.Empty, settings, services, fallback, DefaultContextMenuFactory.Instance);
    }

    // Mirrors fallbacks such as web search, whose title embeds the query.
    private sealed partial class QueryTitleFallback(string id) : FallbackCommandItem(new NoOpCommand(), "Query fallback", id)
    {
        private int _queryCount;

        public event Action? Queried;

        public int QueryCount => Volatile.Read(ref _queryCount);

        // Runs inside UpdateQuery before the title changes, to interleave other calls.
        public Action? DuringQuery { get; set; }

        public Func<string, string> TitleFor { get; init; } = query => query;

        // When set, UpdateQuery assigns the subtitle after the title, like Calculator does.
        public Func<string, string>? SubtitleFor { get; init; }

        public override void UpdateQuery(string query)
        {
            Interlocked.Increment(ref _queryCount);
            DuringQuery?.Invoke();
            Title = TitleFor(query);
            Queried?.Invoke();
            if (SubtitleFor is { } subtitleFor)
            {
                Subtitle = subtitleFor(query);
            }
        }
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }
}
