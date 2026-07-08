// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for the opt-in, privacy-safe main-page search telemetry payload builders. These assert
/// that only non-identifying aggregates are captured (query LENGTH, result count, selected rank,
/// ranker tier) and never the raw query text or item content. The actual telemetry sink is not
/// exercised - only the payload-building logic.
/// </summary>
[TestClass]
public partial class SearchTelemetryTests
{
    private sealed partial class MockListItem : IListItem
    {
        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public ICommand Command => new NoOpCommand();

        public IDetails? Details => null;

        public IIconInfo? Icon => null;

        public string Section => throw new NotImplementedException();

        public ITag[] Tags => throw new NotImplementedException();

        public string TextToSuggest => throw new NotImplementedException();

        public IContextItem[] MoreCommands => throw new NotImplementedException();

#pragma warning disable CS0067 // The event is never used
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067 // The event is never used

        public override string ToString() => Title;
    }

    private static RoScored<IListItem> Scored(IListItem item, int score) => new(item, score);

    [TestMethod]
    public void SearchResultsMessage_CapturesQueryLengthNotText()
    {
        const string query = "hello world";
        var message = MainListPage.BuildSearchResultsMessage(query, resultCount: 4, latencyMs: 12);

        Assert.AreEqual(query.Length, message.QueryLength);
        Assert.AreEqual(4, message.ResultCount);
        Assert.IsFalse(message.NoResults);
        Assert.AreEqual(12UL, message.LatencyMs);
    }

    [TestMethod]
    public void SearchResultsMessage_SetsNoResultsFlagWhenCountIsZero()
    {
        var noResults = MainListPage.BuildSearchResultsMessage("abc", resultCount: 0, latencyMs: 5);
        Assert.IsTrue(noResults.NoResults);
        Assert.AreEqual(0, noResults.ResultCount);

        var hasResults = MainListPage.BuildSearchResultsMessage("abc", resultCount: 3, latencyMs: 5);
        Assert.IsFalse(hasResults.NoResults);
    }

    [TestMethod]
    public void SearchResultsMessage_ClampsNegativeInputs()
    {
        var message = MainListPage.BuildSearchResultsMessage(queryLength: -3, resultCount: -1, latencyMs: -100);

        Assert.AreEqual(0, message.QueryLength);
        Assert.AreEqual(0, message.ResultCount);
        Assert.IsTrue(message.NoResults);
        Assert.AreEqual(0UL, message.LatencyMs);
    }

    [TestMethod]
    public void SearchResultsMessage_HasNoStringFields()
    {
        // A raw query string can only be captured through a string member. Assert there is none,
        // so the payload provably cannot carry the query text.
        var stringProperties = typeof(TelemetrySearchResultsMessage)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .ToList();

        Assert.AreEqual(0, stringProperties.Count, "Search results telemetry must not carry any string (potential query text).");
    }

    [TestMethod]
    public void SelectedMessage_CapturesQueryLengthIndexAndTier()
    {
        const string query = "code";
        var message = MainListPage.BuildSearchSelectedMessage(query, selectedIndex: 2, selectedTier: RankTier.Prefix);

        Assert.AreEqual(query.Length, message.QueryLength);
        Assert.AreEqual(2, message.SelectedIndex);
        Assert.AreEqual(RankTier.Prefix, message.SelectedTier);
    }

    [TestMethod]
    public void SelectedMessage_HasNoStringFields()
    {
        var stringProperties = typeof(TelemetrySearchResultSelectedMessage)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .ToList();

        Assert.AreEqual(0, stringProperties.Count, "Selection telemetry must not carry any string (potential query text or item title).");
    }

    [TestMethod]
    public void ResolveSelectedTier_DerivesTierFromPackedScore()
    {
        var exact = new MockListItem { Title = "Visual Studio" };
        var fuzzy = new MockListItem { Title = "Notepad" };

        var packed = new List<RoScored<IListItem>>
        {
            Scored(exact, MainListRanker.Pack(RankTier.ExactTitle, withinTierScore: 500)),
            Scored(fuzzy, MainListRanker.Pack(RankTier.Fuzzy, withinTierScore: 10)),
        };

        Assert.AreEqual(RankTier.ExactTitle, MainListPage.ResolveSelectedTier(exact, packed, fallbackResults: null));
        Assert.AreEqual(RankTier.Fuzzy, MainListPage.ResolveSelectedTier(fuzzy, packed, fallbackResults: null));
    }

    [TestMethod]
    public void ResolveSelectedTier_ReportsFallbackFloorForCommonFallbacks()
    {
        var fallback = new MockListItem { Title = "Search the web" };

        // Common fallbacks carry small rank-based (non-packed) scores. They must be reported at the
        // fallback floor rather than being decoded as a packed tier.
        var fallbacks = new List<RoScored<IListItem>> { Scored(fallback, 3) };

        Assert.AreEqual(RankTier.FallbackFloor, MainListPage.ResolveSelectedTier(fallback, packedResults: null, fallbacks));
    }

    [TestMethod]
    public void ResolveSelectedTier_ReturnsNoneWhenItemNotFound()
    {
        var known = new MockListItem { Title = "Known" };
        var unknown = new MockListItem { Title = "Unknown" };

        var packed = new List<RoScored<IListItem>> { Scored(known, MainListRanker.Pack(RankTier.Prefix, 1)) };

        Assert.AreEqual(RankTier.None, MainListPage.ResolveSelectedTier(unknown, packed, fallbackResults: null));
    }

    [TestMethod]
    public void ResolveVisibleIndex_SkipsSeparatorsAndReturnsVisibleRank()
    {
        var resultsSeparator = new Separator("Results");
        var fallbacksSeparator = new Separator("Fallbacks");

        var a = new MockListItem { Title = "A" };
        var b = new MockListItem { Title = "B" };
        var c = new MockListItem { Title = "C" };
        var missing = new MockListItem { Title = "Missing" };

        var rendered = new IListItem[] { resultsSeparator, a, b, fallbacksSeparator, c };

        Assert.AreEqual(0, MainListPage.ResolveVisibleIndex(rendered, a, resultsSeparator, fallbacksSeparator));
        Assert.AreEqual(1, MainListPage.ResolveVisibleIndex(rendered, b, resultsSeparator, fallbacksSeparator));
        Assert.AreEqual(2, MainListPage.ResolveVisibleIndex(rendered, c, resultsSeparator, fallbacksSeparator));
        Assert.AreEqual(-1, MainListPage.ResolveVisibleIndex(rendered, missing, resultsSeparator, fallbacksSeparator));
        Assert.AreEqual(-1, MainListPage.ResolveVisibleIndex(null, a, resultsSeparator, fallbacksSeparator));
    }

    [TestMethod]
    public void TierOf_RoundTripsEveryPackedTier()
    {
        foreach (RankTier tier in Enum.GetValues<RankTier>())
        {
            if (tier == RankTier.None)
            {
                continue;
            }

            var packed = MainListRanker.Pack(tier, withinTierScore: 42);
            Assert.AreEqual(tier, MainListRanker.TierOf(packed), $"TierOf should round-trip {tier}.");
        }
    }
}
