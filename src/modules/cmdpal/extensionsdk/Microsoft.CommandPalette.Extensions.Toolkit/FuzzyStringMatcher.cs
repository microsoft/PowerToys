// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ToolGood.Words.Pinyin;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// Inspired by the fuzzy.rs from edit.exe
public static class FuzzyStringMatcher
{
    private const int NoMatchScore = 0;
    private const int StackAllocThreshold = 512;

    /// <summary>
    /// Gets a value indicating whether to support Chinese PinYin.
    /// Automatically enabled when the system UI culture is Simplified Chinese.
    /// </summary>
    public static bool ChinesePinYinSupport { get; internal set; } = IsSimplifiedChinese();

    private static bool IsSimplifiedChinese()
    {
        var culture = CultureInfo.CurrentUICulture;
        return culture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
            || culture.Name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PreparedFuzzyQuery GetOrPrepareThreadCached(string needle, bool removeDiacritics)
    {
        return PreparedFuzzyQueryThreadCache.GetOrPrepare(needle, removeDiacritics);
    }

    /// <summary>
    /// Prepare a query for repeated scoring against many targets.
    /// </summary>
    private static PreparedFuzzyQuery PrepareQuery(string input, bool mayNeedDiacriticsRemoval = false)
        => new(input, precomputeNoDiacritics: mayNeedDiacriticsRemoval);

    // ============================================================
    // Public API
    // ============================================================
    public static int ScoreFuzzy(string needle, string haystack, bool allowNonContiguousMatches = true)
    {
        return ScoreFuzzy(needle, haystack, allowNonContiguousMatches, removeDiacritics: true);
    }

    public static int ScoreFuzzy(string needle, string haystack, bool allowNonContiguousMatches, bool removeDiacritics)
    {
        var query = GetOrPrepareThreadCached(needle, removeDiacritics);
        return ScoreBestVariant(in query, haystack, allowNonContiguousMatches, removeDiacritics);
    }

    public static (int Score, List<int> Positions) ScoreFuzzyWithPositions(string needle, string haystack, bool allowNonContiguousMatches)
    {
        return ScoreFuzzyWithPositions(needle, haystack, allowNonContiguousMatches, removeDiacritics: true);
    }

    public static (int Score, List<int> Positions) ScoreFuzzyWithPositions(
        string needle, string haystack, bool allowNonContiguousMatches, bool removeDiacritics)
    {
        var query = GetOrPrepareThreadCached(needle, removeDiacritics);
        return ScoreBestVariantWithPositions(in query, haystack, allowNonContiguousMatches, removeDiacritics);
    }

    internal static void ClearCache()
    {
        PreparedFuzzyQueryThreadCache.Clear();
    }

    // ============================================================
    // Best-variant selection
    // ============================================================
    [SkipLocalsInit]
    private static int ScoreBestVariant(
        in PreparedFuzzyQuery query,
        string haystack,
        bool allowNonContiguousMatches,
        bool removeDiacritics)
    {
        if (string.IsNullOrEmpty(haystack))
        {
            return NoMatchScore;
        }

        var tLen = haystack.Length;

        // Fold haystack ONCE
        using var tFoldBuffer = new RentedSpan<char>(tLen, stackalloc char[Math.Min(tLen, StackAllocThreshold)]);
        Folding.FoldInto(haystack, removeDiacritics, tFoldBuffer.Span);
        ReadOnlySpan<char> tFold = tFoldBuffer.Span;

        var qFold = query.GetPrimaryFolded(removeDiacritics);
        var best = ScoreCore(query.PrimaryRaw, qFold, haystack, tFold, allowNonContiguousMatches);

        if (!ChinesePinYinSupport || !query.HasSecondary)
        {
            return best;
        }

        var qRawSecondary = query.SecondaryRaw ?? string.Empty;
        var qFoldSecondary = query.GetSecondaryFolded(removeDiacritics);

        best = Math.Max(best, ScoreCore(qRawSecondary, qFoldSecondary, haystack, tFold, allowNonContiguousMatches));

        if (!WordsHelper.HasChinese(haystack))
        {
            return best;
        }

        // Fold PinYin target ONCE
        var tPinYin = WordsHelper.GetPinyin(haystack) ?? string.Empty;
        var tPinYinLen = tPinYin.Length;

        using var tPinYinFoldBuffer = new RentedSpan<char>(tPinYinLen, stackalloc char[Math.Min(tPinYinLen, StackAllocThreshold)]);
        Folding.FoldInto(tPinYin, removeDiacritics, tPinYinFoldBuffer.Span);
        ReadOnlySpan<char> tPinYinFold = tPinYinFoldBuffer.Span;

        best = Math.Max(best, ScoreCore(query.PrimaryRaw, qFold, tPinYin, tPinYinFold, allowNonContiguousMatches));
        best = Math.Max(best, ScoreCore(qRawSecondary, qFoldSecondary, tPinYin, tPinYinFold, allowNonContiguousMatches));

        return best;
    }

    private static (int Score, List<int> Positions) ScoreBestVariantWithPositions(
        in PreparedFuzzyQuery query,
        string haystack,
        bool allowNonContiguousMatches,
        bool removeDiacritics)
    {
        if (string.IsNullOrEmpty(haystack))
        {
            return (NoMatchScore, []);
        }

        var tLen = haystack.Length;

        // Fold haystack ONCE
        using var tFoldBuffer = new RentedSpan<char>(tLen, stackalloc char[Math.Min(tLen, StackAllocThreshold)]);
        Folding.FoldInto(haystack, removeDiacritics, tFoldBuffer.Span);
        ReadOnlySpan<char> tFold = tFoldBuffer.Span;

        var needsPinYin = ChinesePinYinSupport && query.HasSecondary && WordsHelper.HasChinese(haystack);
        var tPinYin = needsPinYin ? (WordsHelper.GetPinyin(haystack) ?? string.Empty) : string.Empty;
        var tPinYinLen = tPinYin.Length;

        // Fold PinYin target if needed
        using var tPinYinFoldBuffer = new RentedSpan<char>(
            needsPinYin ? tPinYinLen : 0,
            needsPinYin ? stackalloc char[Math.Min(tPinYinLen, StackAllocThreshold)] : Span<char>.Empty);

        if (needsPinYin)
        {
            Folding.FoldInto(tPinYin, removeDiacritics, tPinYinFoldBuffer.Span);
        }

        ReadOnlySpan<char> tPinYinFold = tPinYinFoldBuffer.Span;

        var qFoldPrimary = query.GetPrimaryFoldedString(removeDiacritics);

        // (primary query, original haystack) - get score AND positions
        var (bestScore, bestPositions) = ScoreWithPositionsCore(query.PrimaryRaw, qFoldPrimary, haystack, tFold, allowNonContiguousMatches);

        // (primary query, pinyin target) - score only.
        // We only return positions for matches against the original haystack.
        // For Pinyin variants, we typically don't show highlights in the UI since there's
        // no 1:1 mapping back to the original characters' positions.
        if (needsPinYin)
        {
            var score = ScoreCore(query.PrimaryRaw, qFoldPrimary, tPinYin, tPinYinFold, allowNonContiguousMatches);
            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        if (ChinesePinYinSupport && query.HasSecondary)
        {
            var qRawSecondary = query.SecondaryRaw ?? string.Empty;
            var qFoldSecondary = query.GetSecondaryFoldedString(removeDiacritics) ?? string.Empty;

            // (secondary query, original haystack) - get score AND positions
            var (scoreSecondary, positionsSecondary) = ScoreWithPositionsCore(
                qRawSecondary, qFoldSecondary, haystack, tFold, allowNonContiguousMatches);

            if (scoreSecondary > bestScore)
            {
                bestScore = scoreSecondary;
                bestPositions = positionsSecondary;
            }

            // (secondary query, pinyin target) - score only.
            // Highlight positions are not returned for Pinyin variants.
            if (needsPinYin)
            {
                var score = ScoreCore(qRawSecondary, qFoldSecondary, tPinYin, tPinYinFold, allowNonContiguousMatches);
                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        return (bestScore, bestPositions);
    }

    // ============================================================
    // Core scoring
    // ============================================================
    private static int ScoreCore(
        ReadOnlySpan<char> qRaw,
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tRaw,
        ReadOnlySpan<char> tFold,
        bool allowNonContiguousMatches)
    {
        var qLen = qRaw.Length;
        var tLen = tRaw.Length;

        if (qLen == 0 || tLen < qLen || qFold.Length != qLen)
        {
            return NoMatchScore;
        }

        return allowNonContiguousMatches
            ? ScoreNonContiguous(qRaw, qFold, tRaw, tFold, qLen, tLen)
            : ScoreContiguous(qRaw, qFold, tRaw, tFold).Score;
    }

    private static (int Score, List<int> Positions) ScoreWithPositionsCore(
        ReadOnlySpan<char> qRaw,
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tRaw,
        ReadOnlySpan<char> tFold,
        bool allowNonContiguousMatches)
    {
        var qLen = qRaw.Length;
        var tLen = tRaw.Length;

        if (qLen == 0 || tLen < qLen || qFold.Length != qLen)
        {
            return (NoMatchScore, []);
        }

        return allowNonContiguousMatches
            ? ScoreNonContiguousWithPositions(qRaw, qFold, tRaw, tFold, qLen, tLen)
            : ScoreContiguousWithPositions(qRaw, qFold, tRaw, tFold);
    }

    // ============================================================
    // Non-contiguous matching (score only)
    // ============================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    [SkipLocalsInit]
    private static int ScoreNonContiguous(
        ReadOnlySpan<char> qRaw,
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tRaw,
        ReadOnlySpan<char> tFold,
        int qLen,
        int tLen)
    {
        if (!Scoring.CanMatchSubsequence(qFold, tFold))
        {
            return NoMatchScore;
        }

        using var dpBuffer = new RentedSpan<int>(tLen * 2, stackalloc int[Math.Min(tLen * 2, StackAllocThreshold)]);
        var scores = dpBuffer.Span[..tLen];
        var seqLens = dpBuffer.Span.Slice(tLen, tLen);
        scores.Clear();
        seqLens.Clear();

        for (var qi = 0; qi < qLen; qi++)
        {
            var qChar = qRaw[qi];
            var qCharFold = qFold[qi];

            var leftScore = 0;
            var diagScore = 0;
            var diagSeqLen = 0;

            var isFirstRow = qi == 0;
            var tiMax = tLen - qLen + qi;

            for (var ti = 0; ti <= tiMax; ti++)
            {
                var upScore = scores[ti];
                var upSeqLen = seqLens[ti];

                var charScore = 0;
                if (diagScore != 0 || isFirstRow)
                {
                    var tCharFold = tFold[ti];
                    if (qCharFold == tCharFold)
                    {
                        charScore = Scoring.ComputeCharScore(
                            qRawChar: qChar,
                            tHasPrev: ti != 0,
                            tRawCharPrev: ti != 0 ? tRaw[ti - 1] : '\0',
                            tRawCharCurr: tRaw[ti],
                            matchSeqLen: diagSeqLen);
                    }
                }

                var candidateScore = diagScore + charScore;

                if (charScore != 0 && candidateScore >= leftScore)
                {
                    scores[ti] = candidateScore;
                    seqLens[ti] = diagSeqLen + 1;
                }
                else
                {
                    scores[ti] = leftScore;
                    seqLens[ti] = 0;
                }

                leftScore = scores[ti];
                diagScore = upScore;
                diagSeqLen = upSeqLen;
            }

            if (leftScore == 0)
            {
                return NoMatchScore;
            }

            if (qi == qLen - 1)
            {
                return leftScore;
            }
        }

        return scores[tLen - 1];
    }

    // ============================================================
    // Non-contiguous matching (with positions)
    // ============================================================
    private static (int Score, List<int> Positions) ScoreNonContiguousWithPositions(
        ReadOnlySpan<char> qRaw,
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tRaw,
        ReadOnlySpan<char> tFold,
        int qLen,
        int tLen)
    {
        if (!Scoring.CanMatchSubsequence(qFold, tFold))
        {
            return (NoMatchScore, []);
        }

        var areaLong = (long)qLen * tLen;
        if (areaLong is <= 0 or > int.MaxValue)
        {
            return (NoMatchScore, []);
        }

        var area = (int)areaLong;
        var bitCount = (area + 63) >> 6;

        using var bitsBuffer = new RentedSpan<ulong>(bitCount, stackalloc ulong[Math.Min(bitCount, StackAllocThreshold / 8)]);
        bitsBuffer.Span.Clear();

        using var dpBuffer = new RentedSpan<int>(tLen * 2, stackalloc int[Math.Min(tLen * 2, StackAllocThreshold)]);
        var scores = dpBuffer.Span[..tLen];
        var seqLens = dpBuffer.Span.Slice(tLen, tLen);
        scores.Clear();
        seqLens.Clear();

        for (var qi = 0; qi < qLen; qi++)
        {
            var qChar = qRaw[qi];
            var qCharFold = qFold[qi];

            var leftScore = 0;
            var diagScore = 0;
            var diagSeqLen = 0;

            var isFirstRow = qi == 0;
            var rowBase = qi * tLen;

            for (var ti = 0; ti < tLen; ti++)
            {
                var upScore = scores[ti];
                var upSeqLen = seqLens[ti];

                var charScore = 0;
                if (diagScore != 0 || isFirstRow)
                {
                    var tCharFold = tFold[ti];
                    if (qCharFold == tCharFold)
                    {
                        charScore = Scoring.ComputeCharScore(
                            qRawChar: qChar,
                            tHasPrev: ti != 0,
                            tRawCharPrev: ti != 0 ? tRaw[ti - 1] : '\0',
                            tRawCharCurr: tRaw[ti],
                            matchSeqLen: diagSeqLen);
                    }
                }

                var candidateScore = diagScore + charScore;

                if (charScore != 0 && candidateScore >= leftScore)
                {
                    scores[ti] = candidateScore;
                    seqLens[ti] = diagSeqLen + 1;
                    SetBit(bitsBuffer.Span, rowBase + ti);
                }
                else
                {
                    scores[ti] = leftScore;
                    seqLens[ti] = 0;
                }

                leftScore = scores[ti];
                diagScore = upScore;
                diagSeqLen = upSeqLen;
            }

            if (leftScore == 0)
            {
                return (NoMatchScore, []);
            }
        }

        var finalScore = scores[tLen - 1];
        if (finalScore == 0)
        {
            return (NoMatchScore, []);
        }

        // Backtrack to find positions
        var positions = new List<int>(qLen);
        var q = qLen - 1;
        var t = tLen - 1;

        while (true)
        {
            var bitIdx = (q * tLen) + t;

            if (!GetBit(bitsBuffer.Span, bitIdx))
            {
                if (t == 0)
                {
                    break;
                }

                t--;
            }
            else
            {
                positions.Add(t);
                if (q == 0 || t == 0)
                {
                    break;
                }

                q--;
                t--;
            }
        }

        positions.Reverse();
        return (finalScore, positions);
    }

    // ============================================================
    // Contiguous matching
    // ============================================================
    private static (int Score, int Start) ScoreContiguous(
        ReadOnlySpan<char> qRaw,
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tRaw,
        ReadOnlySpan<char> tFold)
    {
        var qLen = qRaw.Length;
        var tLen = tRaw.Length;

        if (qLen == 0 || tLen == 0 || tLen < qLen)
        {
            return (NoMatchScore, -1);
        }

        var bestScore = NoMatchScore;
        var bestStart = -1;
        var searchStart = 0;

        while (searchStart <= tLen - qLen)
        {
            var relativeIdx = tFold.Slice(searchStart).IndexOf(qFold);
            if (relativeIdx < 0)
            {
                break;
            }

            var matchStart = searchStart + relativeIdx;
            var score = 0;

            for (var i = 0; i < qLen; i++)
            {
                var ti = matchStart + i;
                score += Scoring.ComputeCharScore(
                    qRawChar: qRaw[i],
                    tHasPrev: ti != 0,
                    tRawCharPrev: ti != 0 ? tRaw[ti - 1] : '\0',
                    tRawCharCurr: tRaw[ti],
                    matchSeqLen: i);
            }

            if (score >= bestScore)
            {
                bestScore = score;
                bestStart = matchStart;
            }

            searchStart = matchStart + 1;
        }

        return (bestScore, bestStart);
    }

    private static (int Score, List<int> Positions) ScoreContiguousWithPositions(
        ReadOnlySpan<char> qRaw,
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tRaw,
        ReadOnlySpan<char> tFold)
    {
        var (score, bestStart) = ScoreContiguous(qRaw, qFold, tRaw, tFold);

        if (bestStart < 0 || score == NoMatchScore)
        {
            return (NoMatchScore, []);
        }

        var qLen = qRaw.Length;
        var positions = new List<int>(qLen);
        for (var i = 0; i < qLen; i++)
        {
            positions.Add(bestStart + i);
        }

        return (score, positions);
    }

    // ============================================================
    // Bit manipulation helpers
    // ============================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetBit(Span<ulong> words, int idx)
    {
        words[idx >> 6] |= 1UL << (idx & 63);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetBit(ReadOnlySpan<ulong> words, int idx)
    {
        return ((words[idx >> 6] >> (idx & 63)) & 1UL) != 0;
    }

    // ============================================================
    // Scoring helpers
    // ============================================================
    private static class Scoring
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static bool CanMatchSubsequence(ReadOnlySpan<char> qFold, ReadOnlySpan<char> tFold)
        {
            var qi = 0;
            var qLen = qFold.Length;

            foreach (var tChar in tFold)
            {
                if (qi < qLen && qFold[qi] == tChar)
                {
                    qi++;
                }
            }

            return qi == qLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static int ComputeCharScore(
            char qRawChar,
            bool tHasPrev,
            char tRawCharPrev,
            char tRawCharCurr,
            int matchSeqLen)
        {
            var score = Bonus.CharacterMatch;

            if (matchSeqLen > 0)
            {
                score += matchSeqLen * Bonus.ConsecutiveMultiplier;
            }

            var tCharCurrIsUpper = char.IsUpper(tRawCharCurr);
            if (qRawChar == tRawCharCurr)
            {
                score += Bonus.ExactCase;
            }

            if (!tHasPrev)
            {
                return score + Bonus.StringStart;
            }

            var separatorBonus = GetSeparatorBonus(tRawCharPrev);
            if (separatorBonus != 0)
            {
                return score + separatorBonus;
            }

            if (matchSeqLen == 0 && tCharCurrIsUpper)
            {
                return score + Bonus.CamelCase;
            }

            return score;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static int GetSeparatorBonus(char ch)
        {
            return ch switch
            {
                '/' or '\\' => Bonus.PathSeparator,
                '_' or '-' or '.' or ' ' or '\'' or '"' or ':' => Bonus.WordSeparator,
                _ => 0,
            };
        }
    }

    // ============================================================
    // Text folding
    // ============================================================

    // Folding: slash normalization + upper case + optional diacritics stripping
    private static class Folding
    {
        // Cache maps an upper case char to its diacritics-stripped upper case char.
        // '\0' means "not cached yet".
        private static readonly char[] StripCacheUpper = new char[char.MaxValue + 1];

        /// <summary>
        /// Folds <paramref name="input"/> into <paramref name="dest"/>:
        /// - Normalizes slashes: '\' -> '/'
        /// - Upper case with char.ToUpperInvariant (length-preserving)
        /// - Optionally strips diacritics (length-preserving)
        /// </summary>
        public static void FoldInto(ReadOnlySpan<char> input, bool removeDiacritics, Span<char> dest)
        {
            // Assumes dest.Length >= input.Length.
            if (!removeDiacritics)
            {
                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    dest[i] = c == '\\' ? '/' : char.ToUpperInvariant(c);
                }

                return;
            }

            // ASCII cannot have diacritics (and ToUpperInvariant is cheap), but we STILL normalize slashes.
            if (Ascii.IsValid(input))
            {
                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    dest[i] = c == '\\' ? '/' : char.ToUpperInvariant(c);
                }

                return;
            }

            // Non-ASCII + removeDiacritics
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                var upper = c == '\\' ? '/' : char.ToUpperInvariant(c);
                dest[i] = StripDiacriticsFromUpper(upper);
            }
        }

        /// <summary>
        /// Creates a folded string for fast equality comparisons:
        /// - ALWAYS normalizes slashes: '\' -> '/'
        /// - Uppercases with char.ToUpperInvariant (length-preserving)
        /// - Optionally strips diacritics (length-preserving)
        ///
        /// Returns the original <paramref name="input"/> when it is already in the desired form.
        /// </summary>
        public static string FoldForComparison(string input, bool removeDiacritics)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            // If already fully normalized (slashes + casing), return input without allocating.
            // Note: when removeDiacritics==true we still must run diacritics stripping on non-ASCII,
            // so the "no-op" path is only safe if removeDiacritics==false OR input is ASCII.
            if (!removeDiacritics)
            {
                if (IsAlreadyFoldedAndSlashNormalized(input))
                {
                    return input;
                }

                return string.Create(input.Length, input, static (dst, src) =>
                {
                    for (var i = 0; i < src.Length; i++)
                    {
                        var c = src[i];
                        dst[i] = c == '\\' ? '/' : char.ToUpperInvariant(c);
                    }
                });
            }

            // removeDiacritics == true
            if (Ascii.IsValid(input))
            {
                // IMPORTANT: still normalize slashes for ASCII so caller can do simple equality checks.
                if (IsAlreadyFoldedAndSlashNormalized(input))
                {
                    return input;
                }

                return string.Create(input.Length, input, static (dst, src) =>
                {
                    for (var i = 0; i < src.Length; i++)
                    {
                        var c = src[i];
                        dst[i] = c == '\\' ? '/' : char.ToUpperInvariant(c);
                    }
                });
            }

            // Non-ASCII + removeDiacritics: must fold + strip (and still normalize slashes).
            return string.Create(input.Length, input, static (dst, src) =>
            {
                for (var i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    var upper = c == '\\' ? '/' : char.ToUpperInvariant(c);
                    dst[i] = StripDiacriticsFromUpper(upper);
                }
            });
        }

        // ============================================================
        // "No-op" detector (fast, avoids ToUpperInvariant per char for CJK)
        // ============================================================
        private static bool IsAlreadyFoldedAndSlashNormalized(string input)
        {
            var sawNonAscii = false;

            // Tier 1: cheap ASCII checks.
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];

                if (c == '\\')
                {
                    return false;
                }

                // ASCII lowercase present => would change.
                if ((uint)(c - 'a') <= ('z' - 'a'))
                {
                    return false;
                }

                if (c > 0x7F)
                {
                    sawNonAscii = true;
                }
            }

            // Tier 2: only when non-ASCII exists; avoid char.ToUpperInvariant for scripts without case.
            if (sawNonAscii)
            {
                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    if (c <= 0x7F)
                    {
                        continue;
                    }

                    var cat = CharUnicodeInfo.GetUnicodeCategory(c);

                    // Lowercase/Titlecase letters will change under ToUpperInvariant.
                    if (cat is UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // ============================================================
        // Diacritics stripping (cached; input is expected to be uppercase already)
        // ============================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char StripDiacriticsFromUpper(char upper)
        {
            if (upper <= 0x7F)
            {
                return upper;
            }

            var cached = StripCacheUpper[upper];
            if (cached != '\0')
            {
                return cached;
            }

            var mapped = StripDiacriticsSlow(upper);
            StripCacheUpper[upper] = mapped;
            return mapped;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static char StripDiacriticsSlow(char upper)
        {
            var baseChar = FirstNonMark(upper, NormalizationForm.FormD);
            if (baseChar == '\0' || baseChar == upper)
            {
                var kd = FirstNonMark(upper, NormalizationForm.FormKD);
                if (kd != '\0')
                {
                    baseChar = kd;
                }
            }

            // Ensure result remains uppercase invariant.
            return char.ToUpperInvariant(baseChar == '\0' ? upper : baseChar);

            static char FirstNonMark(char c, NormalizationForm form)
            {
                var normalized = c.ToString().Normalize(form);

                foreach (var ch in normalized)
                {
                    var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                    if (cat is not (UnicodeCategory.NonSpacingMark
                        or UnicodeCategory.SpacingCombiningMark
                        or UnicodeCategory.EnclosingMark))
                    {
                        return ch;
                    }
                }

                return '\0';
            }
        }
    }

    // ============================================================
    // Text utilities
    // ============================================================
    private static class Text
    {
        internal static string RemoveApostrophes(ReadOnlySpan<char> input)
        {
            var firstIdx = input.IndexOf('\'');
            if (firstIdx < 0)
            {
                return input.ToString();
            }

            var count = 1;
            for (var i = firstIdx + 1; i < input.Length; i++)
            {
                if (input[i] == '\'')
                {
                    count++;
                }
            }

            return string.Create(input.Length - count, input.ToString(), static (dest, src) =>
            {
                var destIdx = 0;
                foreach (var c in src)
                {
                    if (c != '\'')
                    {
                        dest[destIdx++] = c;
                    }
                }
            });
        }
    }

    // ============================================================
    // Scoring bonuses
    // ============================================================
    private static class Bonus
    {
        public const int CharacterMatch = 1;
        public const int ConsecutiveMultiplier = 5;
        public const int ExactCase = 1;
        public const int StringStart = 8;
        public const int PathSeparator = 5;
        public const int WordSeparator = 4;
        public const int CamelCase = 2;
    }

    // ============================================================
    // Memory management
    // ============================================================
    private ref struct RentedSpan<T>
    {
        private readonly Span<T> _span;
        private T[]? _poolArray;

        public readonly Span<T> Span => _span;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RentedSpan(int length, Span<T> stackBuffer)
        {
            if (length <= stackBuffer.Length)
            {
                _poolArray = null;
                _span = stackBuffer[..length];
            }
            else
            {
                _poolArray = ArrayPool<T>.Shared.Rent(length);
                _span = new Span<T>(_poolArray, 0, length);
            }
        }

        public static implicit operator Span<T>(RentedSpan<T> rented) => rented._span;

        public static implicit operator ReadOnlySpan<T>(RentedSpan<T> rented) => rented._span;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = _poolArray;
            if (toReturn != null)
            {
                _poolArray = null;
                ArrayPool<T>.Shared.Return(toReturn, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }
    }

    // ============================================================
    // Prepared query
    // ============================================================
    private readonly struct PreparedFuzzyQuery
    {
        public readonly string PrimaryRaw;
        internal readonly string? SecondaryRaw;

        internal readonly string PrimaryFolded;
        internal readonly string? PrimaryFoldedNoDiacritics;

        internal readonly string? SecondaryFolded;
        internal readonly string? SecondaryFoldedNoDiacritics;

        internal PreparedFuzzyQuery(string primaryRaw, bool precomputeNoDiacritics)
        {
            PrimaryRaw = primaryRaw ?? string.Empty;

            PrimaryFolded = Folding.FoldForComparison(PrimaryRaw, removeDiacritics: false);
            PrimaryFoldedNoDiacritics = precomputeNoDiacritics
                ? Folding.FoldForComparison(PrimaryRaw, removeDiacritics: true)
                : null;

            if (ChinesePinYinSupport)
            {
                var input = Text.RemoveApostrophes(PrimaryRaw);
                SecondaryRaw = WordsHelper.GetPinyin(input) ?? string.Empty;

                SecondaryFolded = Folding.FoldForComparison(SecondaryRaw, removeDiacritics: false);
                SecondaryFoldedNoDiacritics = precomputeNoDiacritics
                    ? Folding.FoldForComparison(SecondaryRaw, removeDiacritics: true)
                    : null;
            }
            else
            {
                SecondaryRaw = null;
                SecondaryFolded = null;
                SecondaryFoldedNoDiacritics = null;
            }
        }

        internal bool HasSecondary => SecondaryFolded is not null;

        internal string GetPrimaryFoldedString(bool removeDiacritics)
        {
            return !removeDiacritics
                ? PrimaryFolded
                : (PrimaryFoldedNoDiacritics ?? Folding.FoldForComparison(PrimaryRaw, removeDiacritics: true));
        }

        internal string? GetSecondaryFoldedString(bool removeDiacritics)
        {
            if (SecondaryFolded is null)
            {
                return null;
            }

            return !removeDiacritics
                ? SecondaryFolded
                : (SecondaryFoldedNoDiacritics ?? Folding.FoldForComparison(SecondaryRaw ?? string.Empty, removeDiacritics: true));
        }

        internal ReadOnlySpan<char> GetPrimaryFolded(bool removeDiacritics)
        {
            return GetPrimaryFoldedString(removeDiacritics).AsSpan();
        }

        internal ReadOnlySpan<char> GetSecondaryFolded(bool removeDiacritics)
        {
            return GetSecondaryFoldedString(removeDiacritics).AsSpan();
        }
    }

    // ============================================================
    // Thread-local query cache
    // ============================================================
    private static class PreparedFuzzyQueryThreadCache
    {
        [ThreadStatic]
        private static Cache? _cache;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Cache GetCache()
        {
            return _cache ??= new Cache();
        }

        public static void Clear()
        {
            _cache = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PreparedFuzzyQuery GetOrPrepare(string? needle, bool removeDiacritics)
        {
            needle ??= string.Empty;

            var cache = GetCache();

            if (string.Equals(cache.Needle, needle, StringComparison.Ordinal))
            {
                if (!removeDiacritics || cache.HasDiacriticsVersion)
                {
                    return cache.Query;
                }

                cache.Query = PrepareQuery(needle, true);
                cache.HasDiacriticsVersion = true;
                return cache.Query;
            }

            cache.Needle = needle;
            cache.Query = PrepareQuery(needle, removeDiacritics);
            cache.HasDiacriticsVersion = removeDiacritics;
            return cache.Query;
        }

        private sealed class Cache
        {
            public string? Needle { get; set; }

            public PreparedFuzzyQuery Query { get; set; }

            public bool HasDiacriticsVersion { get; set; }
        }
    }
}
