// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Finds snippets by trigger and by text. Trigger matches come first, best first: the exact trigger, then one that
/// starts with the search, then one that contains it, then a fuzzy match; then snippets whose text contains it.
/// Ties keep the file's order.
/// </summary>
internal static class SnippetSearch
{
    internal enum Match
    {
        ExactTrigger,
        ExactTriggerIgnoringCase,
        TriggerStart,
        TriggerContains,
        FuzzyTrigger,
        Text,
        None,
    }

    /// <summary>Gets the snippets that match <paramref name="query"/>, best first, or all of them for an empty search.</summary>
    public static IReadOnlyList<Snippet> Filter(IReadOnlyList<Snippet> snippets, string? query)
        => Filter(snippets, query, Match.Text);

    /// <summary>
    /// Gets the snippet to offer in Command Palette's main search: the best trigger match. Text matches are left to the
    /// snippets page, so a search for something else doesn't turn up a snippet that merely mentions it.
    /// </summary>
    public static Snippet? BestTriggerMatch(IReadOnlyList<Snippet> snippets, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var matches = Filter(snippets, query, Match.TriggerContains);
        return matches.Count > 0 ? matches[0] : null;
    }

    internal static (Match Match, int Score) Score(Snippet snippet, string query)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        var trigger = snippet.Trigger;
        if (trigger.Equals(query, StringComparison.Ordinal))
        {
            return (Match.ExactTrigger, 0);
        }

        if (trigger.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return (Match.ExactTriggerIgnoringCase, 0);
        }

        // Shorter triggers first: they're closer to what was typed.
        if (trigger.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return (Match.TriggerStart, -trigger.Length);
        }

        if (trigger.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return (Match.TriggerContains, -trigger.Length);
        }

        var fuzzy = FuzzyStringMatcher.ScoreFuzzy(query, trigger);
        if (fuzzy > 0)
        {
            return (Match.FuzzyTrigger, fuzzy);
        }

        return snippet.Replacement.Contains(query, StringComparison.OrdinalIgnoreCase) ? (Match.Text, 0) : (Match.None, 0);
    }

    private static IReadOnlyList<Snippet> Filter(IReadOnlyList<Snippet> snippets, string? query, Match weakest)
    {
        ArgumentNullException.ThrowIfNull(snippets);

        if (string.IsNullOrWhiteSpace(query))
        {
            return snippets;
        }

        var search = query.Trim();
        var matches = new List<(Snippet Snippet, Match Match, int Score, int Index)>();
        for (var i = 0; i < snippets.Count; i++)
        {
            var (match, score) = Score(snippets[i], search);
            if (match <= weakest)
            {
                matches.Add((snippets[i], match, score, i));
            }
        }

        matches.Sort(static (a, b) =>
            a.Match != b.Match ? a.Match.CompareTo(b.Match)
            : a.Score != b.Score ? b.Score.CompareTo(a.Score)
            : a.Index.CompareTo(b.Index));

        var result = new Snippet[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            result[i] = matches[i].Snippet;
        }

        return result;
    }
}
