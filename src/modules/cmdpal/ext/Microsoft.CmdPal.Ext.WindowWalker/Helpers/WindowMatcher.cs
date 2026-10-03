// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

/// <summary>
/// Picks the open window that best answers a query typed on the main page.
/// </summary>
/// <remarks>
/// This is stricter than <see cref="WindowSearchScorer"/>, which the Window Walker page uses:
/// the fallback shows up next to every app result, so a scattered fuzzy hit on some unrelated
/// window title would be noise there.
/// </remarks>
internal static class WindowMatcher
{
    /// <summary>
    /// Queries shorter than this match too many windows to be useful.
    /// </summary>
    internal const int MinimumQueryLength = 2;

    internal enum MatchKind
    {
        None = 0,
        Title = 1,
        ProcessName = 2,
    }

    /// <summary>
    /// Classifies how a window matches the query. A process name match ranks above a title match,
    /// so that "excel" prefers Excel over a browser tab titled "Excel tips".
    /// </summary>
    internal static MatchKind GetMatchKind(string query, string title, string? processName)
    {
        var trimmedQuery = query.Trim();
        if (trimmedQuery.Length < MinimumQueryLength)
        {
            return MatchKind.None;
        }

        var processNameWithoutExtension = Path.GetFileNameWithoutExtension(processName ?? string.Empty);
        if (IsContiguousMatch(trimmedQuery, processNameWithoutExtension))
        {
            return MatchKind.ProcessName;
        }

        if (IsContiguousMatch(trimmedQuery, title))
        {
            return MatchKind.Title;
        }

        return MatchKind.None;
    }

    /// <summary>
    /// Returns the index of the best matching window, or -1 if none match. The windows are
    /// expected in z-order, so among equal matches the most recently used window wins.
    /// </summary>
    internal static int FindBestMatch<T>(IReadOnlyList<T> windows, string query, Func<T, string> getTitle, Func<T, string?> getProcessName)
    {
        var bestIndex = -1;
        var bestKind = MatchKind.None;
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            var kind = GetMatchKind(query, getTitle(window), getProcessName(window));
            if (kind > bestKind)
            {
                bestKind = kind;
                bestIndex = i;
                if (kind == MatchKind.ProcessName)
                {
                    break;
                }
            }
        }

        return bestIndex;
    }

    private static bool IsContiguousMatch(string query, string text)
    {
        return !string.IsNullOrEmpty(text)
            && FuzzyStringMatcher.ScoreFuzzy(query, text, allowNonContiguousMatches: false) > 0;
    }
}
