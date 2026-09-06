// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

/// <summary>
/// Scores an open window against a search query, matching the query against both the
/// window title and the owning process name.
/// </summary>
internal static class WindowSearchScorer
{
    /// <summary>
    /// Scores <paramref name="query"/> against a window's <paramref name="title"/> and
    /// <paramref name="processName"/>.
    /// </summary>
    /// <remarks>
    /// A single-word query is scored against each field as a whole, and the better of the two
    /// wins. A multi-word query is additionally scored word by word, with each word free to
    /// match either field, so that queries which name the app and part of its title together
    /// ("word budget") match in any order. Every word must match something for the word-by-word
    /// score to apply; otherwise the whole-query score stands. The result is never lower than
    /// the whole-query score, so anything that matched before still matches. Words are separated
    /// by any Unicode whitespace, and the query is whitespace-normalized before being scored as
    /// a whole.
    /// </remarks>
    /// <param name="query">The user's search text.</param>
    /// <param name="title">The window title.</param>
    /// <param name="processName">The name of the process owning the window.</param>
    /// <returns>A score, where 0 means no match.</returns>
    internal static int Score(string? query, string? title, string? processName)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        title ??= string.Empty;
        processName ??= string.Empty;

        // Split on every kind of Unicode whitespace rather than just the space bar: a pasted
        // query can carry a non-breaking space, and folding that into a word would silently
        // drop the query back to single-word behavior.
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        // Score the normalized query, so whitespace that does not appear in the title verbatim
        // (a non-breaking space, a run of spaces, surrounding padding) cannot by itself defeat
        // a whole-query match.
        var wholeQueryScore = ScoreBothFields(string.Join(' ', words), title, processName);

        if (words.Length < 2)
        {
            return wholeQueryScore;
        }

        var total = 0;
        foreach (var word in words)
        {
            var wordScore = ScoreBothFields(word, title, processName);
            if (wordScore == 0)
            {
                // A word that matches neither field means this window isn't what was asked for.
                return wholeQueryScore;
            }

            total += wordScore;
        }

        // Average rather than sum, to keep the per-word score on the same scale as the
        // whole-query score the two are compared against.
        return Math.Max(wholeQueryScore, total / words.Length);
    }

    private static int ScoreBothFields(string needle, string title, string processName)
        => Math.Max(
            FuzzyStringMatcher.ScoreFuzzy(needle, title),
            FuzzyStringMatcher.ScoreFuzzy(needle, processName));
}
