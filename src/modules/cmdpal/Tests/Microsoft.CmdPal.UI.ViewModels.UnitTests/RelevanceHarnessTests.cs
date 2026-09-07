// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// End-to-end relevance harness for the main/root page ranker. Every case is expressed as a
/// realistic query paired with an ordering constraint (rank-1, or "X must rank above Y") and
/// asserted against the REAL <see cref="MainListPage.ScoreTopLevelItem"/> scoring path, sorted
/// exactly the way the product sorts (positive scores, descending). The intent is to lock in
/// "results seem logical and relevant" as an objective, extendable yardstick: to add a new
/// scenario, drop another entry in the fixture and another constraint in a test.
///
/// Focused, per-tier unit tests for the <see cref="MainListRanker"/> primitives live alongside
/// this harness in <see cref="MainListRankerTests"/>.
/// </summary>
[TestClass]
public partial class RelevanceHarnessTests : CommandPaletteUnitTestBase
{
    // A lightweight top-level-command / installed-app stand-in built on the real ListItem
    // toolkit type, so the harness drives the same scoring path as the product (as the sibling
    // RecentCommandsTests does) rather than a reimplementation. ProviderId lets a test key a
    // per-provider weight lookup; Id is derived deterministically so frecency history can target it.
    private sealed partial class ListItemMock : ListItem
    {
        public ListItemMock(string title, string? subtitle = "", string? givenId = "", string? providerId = "")
        {
            Title = title;
            Subtitle = subtitle ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            Id = string.IsNullOrEmpty(givenId) ? GenerateId() : givenId;
            Command = new NoOpCommand() { Id = Id };
        }

        public string Id { get; }

        public string ProviderId { get; }

        private string GenerateId()
        {
            var result = WyHash64.ComputeHash64(ProviderId + Title + Subtitle, seed: 0);
            return $"{ProviderId}{result}";
        }
    }

    private static IPrecomputedFuzzyMatcher CreateMatcher() =>
        new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    private static RecentCommandsManager EmptyHistory() => new();

    // A representative slice of the main page: installed apps + top-level commands with
    // realistic titles, subtitles and shared prefixes/acronyms. Deliberately includes the
    // "confusable" clusters users complain about (Calc*, Visual Studio *, Command Prompt vs
    // Control Panel) so the ordering constraints below have real competition to beat.
    private static List<ListItemMock> Fixture() => new()
    {
        new("Command Prompt", "Run the classic command interpreter"),
        new("Control Panel", "Adjust your computer's settings"),
        new("Calculator", "Perform calculations"),
        new("Calendar", "View your schedule"),
        new("Visual Studio Code", "Code editing. Redefined."),
        new("Visual Studio 2022", "Full-featured IDE"),
        new("Windows Settings", "Change PC settings"),
        new("Windows Terminal", "Modern terminal for command-line tools"),
        new("Task Manager", "Monitor apps and processes"),
        new("Notepad", "A simple text editor"),
        new("Microsoft Edge", "Browse the web"),
        new("Paint", "Draw and edit images"),
        new("Paint 3D", "Create in three dimensions"),
    };

    // Scores every fixture item for a query through the real product scorer and returns the
    // matched titles in the exact order the product would render them: positive scores only,
    // sorted descending. Mirrors InternalListHelpers.FilterListWithScores and the existing
    // RecentCommandsTests.GetMatches helper.
    private static List<string> Rank(
        string query,
        IEnumerable<ListItemMock> items,
        IRecentCommandsManager? history = null,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
    {
        var matcher = CreateMatcher();
        var q = matcher.PrecomputeQuery(query);
        history ??= EmptyHistory();

        return items
            .Select(item => (item.Title, Score: MainListPage.ScoreTopLevelItem(q, item, history, matcher, providerWeightLookup)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Title)
            .ToList();
    }

    private static void AssertRank1(
        string query,
        string expectedTitle,
        IRecentCommandsManager? history = null,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
    {
        var ranked = Rank(query, Fixture(), history, providerWeightLookup);
        Assert.IsTrue(ranked.Count > 0, $"Query '{query}' should return at least one match");
        Assert.AreEqual(
            expectedTitle,
            ranked[0],
            $"Query '{query}' should surface '{expectedTitle}' at rank 1. Actual order: [{string.Join(", ", ranked)}]");
    }

    private static void AssertRanksAbove(
        string query,
        string higher,
        string lower,
        IRecentCommandsManager? history = null,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
    {
        var ranked = Rank(query, Fixture(), history, providerWeightLookup);
        var higherIndex = ranked.IndexOf(higher);
        var lowerIndex = ranked.IndexOf(lower);

        Assert.IsTrue(higherIndex >= 0, $"Query '{query}' should match '{higher}'. Actual order: [{string.Join(", ", ranked)}]");
        Assert.IsTrue(lowerIndex >= 0, $"Query '{query}' should match '{lower}'. Actual order: [{string.Join(", ", ranked)}]");
        Assert.IsTrue(
            higherIndex < lowerIndex,
            $"Query '{query}' should rank '{higher}' above '{lower}'. Actual order: [{string.Join(", ", ranked)}]");
    }

    // End-to-end cases: the tier ladder, exercised through the real scorer.
    [TestMethod]
    public void EndToEnd_ExactTitleBeatsPrefix()
    {
        // "Paint" is an exact title; "Paint 3D" only has it as a prefix. Exact must win.
        AssertRank1("paint", "Paint");
        AssertRanksAbove("paint", "Paint", "Paint 3D");
    }

    [TestMethod]
    public void EndToEnd_PrefixBeatsWordBoundary()
    {
        // "co" is a title prefix of Command Prompt and Control Panel, but only a word-boundary
        // match for "Code" inside Visual Studio Code. Prefix outranks word-boundary.
        AssertRanksAbove("co", "Command Prompt", "Visual Studio Code");
        AssertRanksAbove("co", "Control Panel", "Visual Studio Code");
    }

    [TestMethod]
    public void EndToEnd_WordBoundaryBeatsFuzzy()
    {
        // "man" starts the word "Manager" in Task Manager (word-boundary), but is only a loose
        // subsequence (m..a..n) of "Command Prompt" (fuzzy). Word-boundary must win.
        AssertRank1("man", "Task Manager");
        AssertRanksAbove("man", "Task Manager", "Command Prompt");
    }

    [TestMethod]
    public void EndToEnd_AcronymSurfacesTheRightApp()
    {
        // "vsc" is the acronym of Visual Studio Code (V-S-C); Visual Studio 2022 (V-S-2) is not
        // a match. The acronym should surface the obviously-right app at rank 1.
        AssertRank1("vsc", "Visual Studio Code");
    }

    [TestMethod]
    public void EndToEnd_ComplaintCase_SingleLetterSurfacesFrecentApp()
    {
        // "c" prefixes several apps (Calculator, Calendar, Command Prompt, Control Panel). With
        // no signal they tie; a user who keeps opening Calculator should see it at rank 1. This
        // is the canonical "the thing I want is buried" complaint, fixed by within-tier frecency.
        var history = EmptyHistory();
        var calculatorId = Fixture().First(i => i.Title == "Calculator").Id;
        for (var i = 0; i < 5; i++)
        {
            history = history.WithHistoryItem(calculatorId);
        }

        AssertRank1("c", "Calculator", history);
    }

    [TestMethod]
    public void EndToEnd_ComplaintCase_CodeSurfacesVsCode()
    {
        // Typing "code" should put Visual Studio Code first (word-boundary on "Code").
        AssertRank1("code", "Visual Studio Code");
    }

    [TestMethod]
    public void EndToEnd_ComplaintCase_SetSurfacesSettings()
    {
        // Typing "set" should put Windows Settings first (word-boundary on "Settings").
        AssertRank1("set", "Windows Settings");
    }

    [TestMethod]
    public void EndToEnd_FrecencyReordersWithinTierOnly()
    {
        // Heavy use of Visual Studio Code (a word-boundary match for "co") must NOT lift it over
        // Command Prompt / Control Panel, which are prefix matches a whole tier above it.
        // Frecency reorders within a tier; it can never cross a tier boundary.
        var history = EmptyHistory();
        var vsCodeId = Fixture().First(i => i.Title == "Visual Studio Code").Id;
        for (var i = 0; i < 50; i++)
        {
            history = history.WithHistoryItem(vsCodeId);
        }

        AssertRanksAbove("co", "Command Prompt", "Visual Studio Code", history);
        AssertRanksAbove("co", "Control Panel", "Visual Studio Code", history);
    }

    [TestMethod]
    public void EndToEnd_FrecencyBreaksTieWithinTier()
    {
        // "vs" is an acronym match for both Visual Studio Code and Visual Studio 2022 (same
        // tier). With no history they tie; the recently/repeatedly used one should climb to the
        // top of the tier.
        var fixture = Fixture();
        var vs2022Id = fixture.First(i => i.Title == "Visual Studio 2022").Id;

        var history = EmptyHistory();
        for (var i = 0; i < 5; i++)
        {
            history = history.WithHistoryItem(vs2022Id);
        }

        AssertRanksAbove("vs", "Visual Studio 2022", "Visual Studio Code", history);
    }

    [TestMethod]
    public void EndToEnd_ProviderHigherBreaksAnExactTie()
    {
        // Two providers surface an identically-titled "Settings" command. Everything else being
        // equal (same tier, same lexical quality, no frecency), a provider marked Higher should
        // sort above the Normal one. Provider weight is a within-tier nudge for near-ties only.
        var alpha = new ListItemMock("Settings", "From provider Alpha", providerId: "alpha");
        var bravo = new ListItemMock("Settings", "From provider Bravo", providerId: "bravo");
        var items = new List<ListItemMock> { alpha, bravo };

        var matcher = CreateMatcher();
        var q = matcher.PrecomputeQuery("Settings");
        var history = EmptyHistory();

        // Baseline: with no provider weighting the two tie exactly.
        var baseAlpha = MainListPage.ScoreTopLevelItem(q, alpha, history, matcher);
        var baseBravo = MainListPage.ScoreTopLevelItem(q, bravo, history, matcher);
        Assert.AreEqual(baseAlpha, baseBravo, "The two identically-titled items should tie before provider weighting");

        Func<IListItem, ProviderSearchWeight> lookup = item =>
            item is ListItemMock m && m.ProviderId == "bravo"
                ? ProviderSearchWeight.Higher
                : ProviderSearchWeight.Normal;

        var ranked = items
            .Select(item => (item.ProviderId, Score: MainListPage.ScoreTopLevelItem(q, item, history, matcher, lookup)))
            .OrderByDescending(x => x.Score)
            .Select(x => x.ProviderId)
            .ToList();

        Assert.AreEqual("bravo", ranked[0], "The Higher-weighted provider should win an otherwise exact tie");
    }

    [TestMethod]
    public void EndToEnd_ProviderWeightCannotCrossTierBoundary()
    {
        // Even marked Higher, a word-boundary match (Visual Studio Code for "co") must stay
        // below a prefix match (Command Prompt). Provider weight is clamped within a tier.
        Func<IListItem, ProviderSearchWeight> boostVsCode = item =>
            item is ListItemMock m && m.Title == "Visual Studio Code"
                ? ProviderSearchWeight.Higher
                : ProviderSearchWeight.Normal;

        AssertRanksAbove("co", "Command Prompt", "Visual Studio Code", providerWeightLookup: boostVsCode);
    }
}
