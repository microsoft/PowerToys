// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

internal static class FallbackRenderBlockFactory
{
    internal static FallbackRenderBlock Create(
        IReadOnlyList<IListItem> sourceItems,
        bool treatAsGlobal,
        int score,
        bool showResultsInDedicatedSection,
        bool showBeforeMainResults,
        Separator? sectionSeparator,
        in FuzzyQuery searchQuery,
        ScoringFunction<IListItem> scoringFunction)
    {
        var visibleItems = sourceItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .ToArray();

        if (visibleItems.Length == 0)
        {
            return FallbackRenderBlock.Empty;
        }

        if (showBeforeMainResults)
        {
            return new FallbackRenderBlock(
                LeadingItems: CreateSectionItems(sectionSeparator, visibleItems),
                ScoredGlobalItems: [],
                TrailingGlobalItems: [],
                OrderedFallbackItems: []);
        }

        if (treatAsGlobal)
        {
            return showResultsInDedicatedSection
                ? new FallbackRenderBlock(
                    LeadingItems: [],
                    ScoredGlobalItems: [],
                    TrailingGlobalItems: CreateSectionItems(sectionSeparator, visibleItems),
                    OrderedFallbackItems: [])
                : new FallbackRenderBlock(
                    LeadingItems: [],
                    ScoredGlobalItems: [.. InternalListHelpers.FilterListWithScores(visibleItems, searchQuery, scoringFunction)],
                    TrailingGlobalItems: [],
                    OrderedFallbackItems: []);
        }

        var orderedItems = new List<RoScored<IListItem>>(visibleItems.Length + (showResultsInDedicatedSection && sectionSeparator is not null ? 1 : 0));
        if (showResultsInDedicatedSection && sectionSeparator is not null)
        {
            orderedItems.Add(new(sectionSeparator, score));
        }

        foreach (var item in visibleItems)
        {
            orderedItems.Add(new(item, score));
        }

        return new FallbackRenderBlock(
            LeadingItems: [],
            ScoredGlobalItems: [],
            TrailingGlobalItems: [],
            OrderedFallbackItems: [.. orderedItems]);
    }

    private static IListItem[] CreateSectionItems(Separator? sectionSeparator, IReadOnlyList<IListItem> items)
    {
        if (sectionSeparator is null)
        {
            return [.. items];
        }

        var sectionItems = new IListItem[items.Count + 1];
        sectionItems[0] = sectionSeparator;
        for (var i = 0; i < items.Count; i++)
        {
            sectionItems[i + 1] = items[i];
        }

        return sectionItems;
    }
}

internal readonly record struct FallbackRenderBlock(
    IListItem[] LeadingItems,
    RoScored<IListItem>[] ScoredGlobalItems,
    IListItem[] TrailingGlobalItems,
    RoScored<IListItem>[] OrderedFallbackItems)
{
    internal static readonly FallbackRenderBlock Empty = new([], [], [], []);
}
