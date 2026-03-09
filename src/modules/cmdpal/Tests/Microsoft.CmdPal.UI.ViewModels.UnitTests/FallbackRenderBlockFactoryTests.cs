// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class FallbackRenderBlockFactoryTests
{
    private static readonly PrecomputedFuzzyMatcher Matcher = new(new PrecomputedFuzzyMatcherOptions());
    private static readonly string[] ExpectedFileSectionItems = ["Files", "file-1.txt", "file-2.txt"];
    private static readonly string[] ExpectedLeadingFileSectionItems = ["Files", "file-0.txt"];
    private static readonly int[] ExpectedRankedScores = [7, 7, 7];

    [TestMethod]
    public void Create_GlobalInline_ReturnsOnlyScoredGlobalItems()
    {
        var query = Matcher.PrecomputeQuery("file");
        IListItem[] items =
        [
            new MockListItem("file-1.txt"),
            new MockListItem("other"),
            new MockListItem(string.Empty),
        ];

        var block = FallbackRenderBlockFactory.Create(
            items,
            treatAsGlobal: true,
            score: 0,
            showResultsInDedicatedSection: false,
            showBeforeMainResults: false,
            sectionSeparator: null,
            query,
            ScoreByContains);

        Assert.AreEqual(0, block.LeadingItems.Length);
        Assert.AreEqual(1, block.ScoredGlobalItems.Length);
        Assert.AreEqual("file-1.txt", block.ScoredGlobalItems[0].Item.Title);
        Assert.AreEqual(0, block.TrailingGlobalItems.Length);
        Assert.AreEqual(0, block.OrderedFallbackItems.Length);
    }

    [TestMethod]
    public void Create_GlobalDedicatedSection_ReturnsTrailingSectionItems()
    {
        var query = Matcher.PrecomputeQuery("file");
        IListItem[] items =
        [
            new MockListItem("file-1.txt"),
            new MockListItem("file-2.txt"),
        ];

        var block = FallbackRenderBlockFactory.Create(
            items,
            treatAsGlobal: true,
            score: 0,
            showResultsInDedicatedSection: true,
            showBeforeMainResults: false,
            sectionSeparator: new Separator("Files"),
            query,
            ScoreByContains);

        CollectionAssert.AreEqual(
            ExpectedFileSectionItems,
            block.TrailingGlobalItems.Select(item => item.Title).ToArray());
    }

    [TestMethod]
    public void Create_LeadingSection_ReturnsLeadingItems()
    {
        var query = Matcher.PrecomputeQuery("file");
        IListItem[] items =
        [
            new MockListItem("file-0.txt"),
        ];

        var block = FallbackRenderBlockFactory.Create(
            items,
            treatAsGlobal: true,
            score: 0,
            showResultsInDedicatedSection: true,
            showBeforeMainResults: true,
            sectionSeparator: new Separator("Files"),
            query,
            ScoreByContains);

        CollectionAssert.AreEqual(
            ExpectedLeadingFileSectionItems,
            block.LeadingItems.Select(item => item.Title).ToArray());
    }

    [TestMethod]
    public void Create_RankedDedicatedSection_ReturnsOrderedItems()
    {
        var query = Matcher.PrecomputeQuery("file");
        IListItem[] items =
        [
            new MockListItem("file-1.txt"),
            new MockListItem("file-2.txt"),
        ];

        var block = FallbackRenderBlockFactory.Create(
            items,
            treatAsGlobal: false,
            score: 7,
            showResultsInDedicatedSection: true,
            showBeforeMainResults: false,
            sectionSeparator: new Separator("Files"),
            query,
            ScoreByContains);

        CollectionAssert.AreEqual(
            ExpectedFileSectionItems,
            block.OrderedFallbackItems.Select(item => item.Item.Title).ToArray());
        CollectionAssert.AreEqual(
            ExpectedRankedScores,
            block.OrderedFallbackItems.Select(item => item.Score).ToArray());
    }

    private static int ScoreByContains(in FuzzyQuery query, IListItem item)
    {
        return item.Title.Contains(query.Original, StringComparison.OrdinalIgnoreCase)
            ? item.Title.Length
            : 0;
    }

    private sealed partial class MockListItem : ListItem
    {
        public MockListItem(string title)
            : base(new NoOpCommand())
        {
            Title = title;
        }
    }
}
