// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// High-performance fuzzy string matcher with precomputation support.
/// </summary>
public static class FuzzyStringMatcher
{
    private const int StackAllocThreshold = 256;

    public static int ScoreFuzzy(string needle, string haystack, bool allowNonContiguous = true)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle) || haystack.Length < needle.Length)
        {
            return 0;
        }

        var queryNorm = NormalizeString(needle);
        var targetNorm = NormalizeString(haystack);
        var boundaryMask = ComputeBoundaryMaskSimple(haystack);

        return MatchCore(needle, queryNorm, haystack, targetNorm, boundaryMask, allowNonContiguous);
    }

    public static (int Score, int[] Positions) ScoreFuzzyWithPositions(string needle, string haystack, bool allowNonContiguous = true)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle) || haystack.Length < needle.Length)
        {
            return (0, []);
        }

        var queryNorm = NormalizeString(needle);
        var targetNorm = NormalizeString(haystack);
        var boundaryMask = ComputeBoundaryMaskSimple(haystack);

        var result = MatchCoreWithPositions(needle, queryNorm, haystack, targetNorm, boundaryMask, allowNonContiguous);
        return (result.Score, result.Positions);
    }

    /// <summary>
    /// Match a precomputed query against a precomputed target.
    /// This is the fastest path for repeated searches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Match(in FuzzyMatchQuery query, in FuzzyMatchTarget target, bool allowNonContiguous = true)
    {
        return query.CouldMatch(target)
            ? MatchCore(query.Original, query.Normalized, target.Original, target.Normalized, target.BoundaryMask, allowNonContiguous)
            : 0;
    }

    /// <summary>
    /// Match with position tracking for highlighting.
    /// </summary>
    public static FuzzyMatchResult MatchWithPositions(in FuzzyMatchQuery query, in FuzzyMatchTarget target, bool allowNonContiguous = true)
    {
        return query.CouldMatch(target)
            ? MatchCoreWithPositions(query.Original, query.Normalized, target.Original, target.Normalized, target.BoundaryMask, allowNonContiguous)
            : FuzzyMatchResult.NoMatch;
    }

    private static int MatchCore(
        string queryOrig,
        string queryNorm,
        string targetOrig,
        string targetNorm,
        ulong boundaryMask,
        bool allowNonContiguous)
    {
        var queryLength = queryOrig.Length;
        var targetLength = targetOrig.Length;
        var area = queryLength * targetLength;

        var scoresArray = ArrayPool<int>.Shared.Rent(area);
        var matchesArray = ArrayPool<int>.Shared.Rent(area);

        try
        {
            var scores = scoresArray.AsSpan(0, area);
            var matches = matchesArray.AsSpan(0, area);
            scores.Clear();
            matches.Clear();

            ComputeScoresOptimized(
                queryOrig.AsSpan(),
                queryNorm.AsSpan(),
                targetOrig.AsSpan(),
                targetNorm.AsSpan(),
                boundaryMask,
                scores,
                matches,
                allowNonContiguous);

            return scores[area - 1];
        }
        finally
        {
            ArrayPool<int>.Shared.Return(scoresArray);
            ArrayPool<int>.Shared.Return(matchesArray);
        }
    }

    private static FuzzyMatchResult MatchCoreWithPositions(
        string queryOrig,
        string queryNorm,
        string targetOrig,
        string targetNorm,
        ulong boundaryMask,
        bool allowNonContiguous)
    {
        var queryLength = queryOrig.Length;
        var targetLength = targetOrig.Length;
        var area = queryLength * targetLength;

        var scoresArray = ArrayPool<int>.Shared.Rent(area);
        var matchesArray = ArrayPool<int>.Shared.Rent(area);

        try
        {
            var scores = scoresArray.AsSpan(0, area);
            var matches = matchesArray.AsSpan(0, area);
            scores.Clear();
            matches.Clear();

            ComputeScoresOptimized(
                queryOrig.AsSpan(),
                queryNorm.AsSpan(),
                targetOrig.AsSpan(),
                targetNorm.AsSpan(),
                boundaryMask,
                scores,
                matches,
                allowNonContiguous);

            var positions = ExtractPositions(matches, queryLength, targetLength);
            return new FuzzyMatchResult(scores[area - 1], positions);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(scoresArray);
            ArrayPool<int>.Shared.Return(matchesArray);
        }
    }

    private static void ComputeScoresOptimized(
        ReadOnlySpan<char> queryOrig,
        ReadOnlySpan<char> queryNorm,
        ReadOnlySpan<char> targetOrig,
        ReadOnlySpan<char> targetNorm,
        ulong boundaryMask,
        Span<int> scores,
        Span<int> matches,
        bool allowNonContiguous)
    {
        var queryLength = queryOrig.Length;
        var targetLength = targetOrig.Length;

        for (var qi = 0; qi < queryLength; qi++)
        {
            var qiOffset = qi * targetLength;
            var qiPrevOffset = (qi - 1) * targetLength;
            var queryChar = queryOrig[qi];
            var queryNormChar = queryNorm[qi];
            var isFirstQuery = qi == 0;

            for (var ti = 0; ti < targetLength; ti++)
            {
                var currentIndex = qiOffset + ti;
                var leftScore = ti > 0 ? scores[currentIndex - 1] : 0;

                int diagScore, matchSeqLen;
                if (!isFirstQuery && ti > 0)
                {
                    var diagIndex = qiPrevOffset + ti - 1;
                    diagScore = scores[diagIndex];
                    matchSeqLen = matches[diagIndex];
                }
                else
                {
                    diagScore = 0;
                    matchSeqLen = 0;
                }

                if (diagScore == 0 && !isFirstQuery)
                {
                    scores[currentIndex] = leftScore;
                    continue;
                }

                var targetNormChar = targetNorm[ti];

                if (!CharsMatch(queryNormChar, targetNormChar))
                {
                    scores[currentIndex] = leftScore;
                    continue;
                }

                var score = ComputeMatchScoreWithBoundary(
                    queryChar,
                    ti > 0 ? targetOrig[ti - 1] : '\0',
                    ti > 0,
                    targetOrig[ti],
                    matchSeqLen,
                    ti,
                    boundaryMask);

                var totalScore = diagScore + score;

                if (totalScore >= leftScore &&
                    (allowNonContiguous || !isFirstQuery ||
                     targetNorm.Slice(ti).StartsWith(queryNorm)))
                {
                    matches[currentIndex] = matchSeqLen + 1;
                    scores[currentIndex] = totalScore;
                }
                else
                {
                    scores[currentIndex] = leftScore;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeMatchScoreWithBoundary(
        char queryChar,
        char targetPrev,
        bool hasTargetPrev,
        char targetCurr,
        int matchSeqLen,
        int position,
        ulong boundaryMask)
    {
        var score = 1;

        if (matchSeqLen > 0)
        {
            score += matchSeqLen * 5;
        }

        if (queryChar == targetCurr)
        {
            score += 1;
        }

        // Use precomputed boundary mask for positions < 64
        if (position < 64 && (boundaryMask & (1UL << position)) != 0)
        {
            score += position == 0 ? 8 : 4; // Start bonus vs separator bonus
        }
        else if (hasTargetPrev)
        {
            // Fallback for positions >= 64 or non-boundary
            if (char.IsUpper(targetCurr) && char.IsLower(targetPrev) && matchSeqLen == 0)
            {
                score += 2; // CamelCase
            }
        }
        else
        {
            score += 8; // Start of string
        }

        return score;
    }

    private static int[] ExtractPositions(ReadOnlySpan<int> matches, int queryLength, int targetLength)
    {
        if (queryLength == 0 || targetLength == 0)
        {
            return [];
        }

        var tempPositions = queryLength <= StackAllocThreshold
            ? stackalloc int[queryLength]
            : new int[queryLength];

        var posCount = 0;
        var qi = queryLength - 1;
        var ti = targetLength - 1;

        while (qi >= 0 && ti >= 0)
        {
            var index = (qi * targetLength) + ti;
            if (matches[index] == 0)
            {
                ti--;
            }
            else
            {
                tempPositions[posCount++] = ti;
                qi--;
                ti--;
            }
        }

        if (posCount == 0)
        {
            return [];
        }

        var positions = new int[posCount];
        for (var i = 0; i < posCount; i++)
        {
            positions[i] = tempPositions[posCount - 1 - i];
        }

        return positions;
    }

    /// <summary>
    /// Normalize a string for fuzzy matching (public for precomputation).
    /// </summary>
    public static string NormalizeString(string input)
    {
        return FuzzyStringMatchNormalizationHelper.NormalizeString(input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CharsMatch(char a, char b) => a == b || (a is '/' or '\\' && b is '/' or '\\');

    private static ulong ComputeBoundaryMaskSimple(string original)
    {
        if (original.Length == 0)
        {
            return 0;
        }

        ulong mask = 1;
        var limit = Math.Min(original.Length, 64);

        for (var i = 1; i < limit; i++)
        {
            var prev = original[i - 1];
            var curr = original[i];

            if (IsSeparator(prev) ||
                (char.IsLower(prev) && char.IsUpper(curr)) ||
                (char.IsLetter(prev) && char.IsDigit(curr)) ||
                (char.IsDigit(prev) && char.IsLetter(curr)))
            {
                mask |= 1UL << i;
            }
        }

        return mask;

        static bool IsSeparator(char c) =>
            c is '/' or '\\' or '_' or '-' or '.' or ' ' or '\'' or '"' or ':';
    }
}
