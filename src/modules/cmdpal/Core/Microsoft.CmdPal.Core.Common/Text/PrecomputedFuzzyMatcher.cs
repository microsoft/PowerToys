// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class PrecomputedFuzzyMatcher : IPrecomputedFuzzyMatcher
{
    private const int NoMatchScore = 0;
    private const int StackallocThresholdChars = 512;
    private const int FolderSchemaVersion = 1;
    private const int BloomSchemaVersion = 1;

    private readonly PrecomputedFuzzyMatcherOptions _options;
    private readonly IStringFolder _stringFolder;
    private readonly IBloomFilter _bloom;

    public PrecomputedFuzzyMatcher(
        PrecomputedFuzzyMatcherOptions? options = null,
        IStringFolder? normalization = null,
        IBloomFilter? bloomCalculator = null)
    {
        _options = options ?? PrecomputedFuzzyMatcherOptions.Default;
        _bloom = bloomCalculator ?? new BloomFilter();
        _stringFolder = normalization ?? new StringFolder();

        SchemaId = ComputeSchemaId(_options);
    }

    public uint SchemaId { get; }

    public FuzzyQuery PrecomputeQuery(string? input) => PrecomputeQuery(input, null);

    public FuzzyTarget PrecomputeTarget(string? input) => PrecomputeTarget(input, null);

    public int Score(in FuzzyQuery query, in FuzzyTarget target)
    {
        var qFold = query.FoldedSpan;
        var tLen = target.Length;

        if (query.EffectiveLength == 0 || tLen == 0)
        {
            return NoMatchScore;
        }

        var skipWordSeparators = _options.SkipWordSeparators;
        var bestScore = 0;

        // 1. Primary → Primary
        if (tLen >= query.EffectiveLength && _bloom.MightContain(target.Bloom, query.Bloom))
        {
            if (CanMatchSubsequence(qFold, target.FoldedSpan, skipWordSeparators))
            {
                bestScore = ScoreNonContiguous(
                    qRaw: query.OriginalSpan,
                    qFold: qFold,
                    qEffectiveLen: query.EffectiveLength,
                    tRaw: target.OriginalSpan,
                    tFold: target.FoldedSpan,
                    ignoreSameCaseBonusForThisQuery: _options.IgnoreSameCaseBonusIfQueryIsAllLowercase && query.IsAllLowercaseAsciiOrNonLetter);
            }
        }

        // 2. Secondary → Secondary
        if (query.HasSecondary && target.HasSecondary)
        {
            var qSecFold = query.SecondaryFoldedSpan;

            if (target.SecondaryLength >= query.SecondaryEffectiveLength &&
                _bloom.MightContain(target.SecondaryBloom, query.SecondaryBloom) &&
                CanMatchSubsequence(qSecFold, target.SecondaryFoldedSpan, skipWordSeparators))
            {
                var score = ScoreNonContiguous(
                    qRaw: query.SecondaryOriginalSpan,
                    qFold: qSecFold,
                    qEffectiveLen: query.SecondaryEffectiveLength,
                    tRaw: target.SecondaryOriginalSpan,
                    tFold: target.SecondaryFoldedSpan,
                    ignoreSameCaseBonusForThisQuery: _options.IgnoreSameCaseBonusIfQueryIsAllLowercase && query.SecondaryIsAllLowercaseAsciiOrNonLetter);

                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        // 3. Primary query → Secondary target
        if (target.HasSecondary &&
            target.SecondaryLength >= query.EffectiveLength &&
            _bloom.MightContain(target.SecondaryBloom, query.Bloom))
        {
            if (CanMatchSubsequence(qFold, target.SecondaryFoldedSpan, skipWordSeparators))
            {
                var score = ScoreNonContiguous(
                    qRaw: query.OriginalSpan,
                    qFold: qFold,
                    qEffectiveLen: query.EffectiveLength,
                    tRaw: target.SecondaryOriginalSpan,
                    tFold: target.SecondaryFoldedSpan,
                    ignoreSameCaseBonusForThisQuery: _options.IgnoreSameCaseBonusIfQueryIsAllLowercase && query.IsAllLowercaseAsciiOrNonLetter);

                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        // 4. Secondary query → Primary target
        if (query.HasSecondary &&
            tLen >= query.SecondaryEffectiveLength &&
            _bloom.MightContain(target.Bloom, query.SecondaryBloom))
        {
            var qSecFold = query.SecondaryFoldedSpan;

            if (CanMatchSubsequence(qSecFold, target.FoldedSpan, skipWordSeparators))
            {
                var score = ScoreNonContiguous(
                    qRaw: query.SecondaryOriginalSpan,
                    qFold: qSecFold,
                    qEffectiveLen: query.SecondaryEffectiveLength,
                    tRaw: target.OriginalSpan,
                    tFold: target.FoldedSpan,
                    ignoreSameCaseBonusForThisQuery: _options.IgnoreSameCaseBonusIfQueryIsAllLowercase && query.SecondaryIsAllLowercaseAsciiOrNonLetter);

                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        return bestScore;
    }

    private FuzzyQuery PrecomputeQuery(string? input, string? secondaryInput)
    {
        input ??= string.Empty;

        var folded = _stringFolder.Fold(input, _options.RemoveDiacritics);
        var bloom = _bloom.Compute(folded);
        var effectiveLength = _options.SkipWordSeparators
            ? folded.Length - CountWordSeparators(folded)
            : folded.Length;

        var isAllLowercase = IsAllLowercaseAsciiOrNonLetter(input);

        string? secondaryOriginal = null;
        string? secondaryFolded = null;
        ulong secondaryBloom = 0;
        var secondaryEffectiveLength = 0;
        var secondaryIsAllLowercase = true;

        if (!string.IsNullOrEmpty(secondaryInput))
        {
            secondaryOriginal = secondaryInput;
            secondaryFolded = _stringFolder.Fold(secondaryInput, _options.RemoveDiacritics);
            secondaryBloom = _bloom.Compute(secondaryFolded);
            secondaryEffectiveLength = _options.SkipWordSeparators
                ? secondaryFolded.Length - CountWordSeparators(secondaryFolded)
                : secondaryFolded.Length;

            secondaryIsAllLowercase = IsAllLowercaseAsciiOrNonLetter(secondaryInput);
        }

        return new FuzzyQuery(
            original: input,
            folded: folded,
            bloom: bloom,
            effectiveLength: effectiveLength,
            isAllLowercaseAsciiOrNonLetter: isAllLowercase,
            secondaryOriginal: secondaryOriginal,
            secondaryFolded: secondaryFolded,
            secondaryBloom: secondaryBloom,
            secondaryEffectiveLength: secondaryEffectiveLength,
            secondaryIsAllLowercaseAsciiOrNonLetter: secondaryIsAllLowercase);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CountWordSeparators(string s)
        {
            var count = 0;
            foreach (var c in s)
            {
                if (SymbolClassifier.Classify(c) == SymbolKind.WordSeparator)
                {
                    count++;
                }
            }

            return count;
        }
    }

    internal FuzzyTarget PrecomputeTarget(string? input, string? secondaryInput)
    {
        input ??= string.Empty;

        var folded = _stringFolder.Fold(input, _options.RemoveDiacritics);
        var bloom = _bloom.Compute(folded);

        string? secondaryFolded = null;
        ulong secondaryBloom = 0;

        if (!string.IsNullOrEmpty(secondaryInput))
        {
            secondaryFolded = _stringFolder.Fold(secondaryInput, _options.RemoveDiacritics);
            secondaryBloom = _bloom.Compute(secondaryFolded);
        }

        return new FuzzyTarget(
            input,
            folded,
            bloom,
            secondaryInput,
            secondaryFolded,
            secondaryBloom);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllLowercaseAsciiOrNonLetter(string s)
    {
        foreach (var c in s)
        {
            if ((uint)(c - 'A') <= ('Z' - 'A'))
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanMatchSubsequence(
        ReadOnlySpan<char> qFold,
        ReadOnlySpan<char> tFold,
        bool skipWordSeparators)
    {
        var qi = 0;
        var ti = 0;

        while (qi < qFold.Length && ti < tFold.Length)
        {
            var qChar = qFold[qi];

            if (skipWordSeparators && SymbolClassifier.Classify(qChar) == SymbolKind.WordSeparator)
            {
                qi++;
                continue;
            }

            if (qChar == tFold[ti])
            {
                qi++;
            }

            ti++;
        }

        // Skip trailing word separators in query
        if (skipWordSeparators)
        {
            while (qi < qFold.Length && SymbolClassifier.Classify(qFold[qi]) == SymbolKind.WordSeparator)
            {
                qi++;
            }
        }

        return qi == qFold.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [SkipLocalsInit]
    private int ScoreNonContiguous(
        scoped in ReadOnlySpan<char> qRaw,
        scoped in ReadOnlySpan<char> qFold,
        int qEffectiveLen,
        scoped in ReadOnlySpan<char> tRaw,
        scoped in ReadOnlySpan<char> tFold,
        bool ignoreSameCaseBonusForThisQuery)
    {
        Debug.Assert(qRaw.Length == qFold.Length, "Original and folded spans are traversed in lockstep: requires qRaw.Length == qFold.Length");
        Debug.Assert(tRaw.Length == tFold.Length, "Original and folded spans are traversed in lockstep: requires tRaw.Length == tFold.Length");
        Debug.Assert(qEffectiveLen <= qFold.Length, "Effective length must be less than or equal to folded length");

        var qLen = qFold.Length;
        var tLen = tFold.Length;

        // Copy options to local variables to avoid repeated field accesses
        var charMatchBonus = _options.CharMatchBonus;
        var sameCaseBonus = ignoreSameCaseBonusForThisQuery ? 0 : _options.SameCaseBonus;
        var consecutiveMultiplier = _options.ConsecutiveMultiplier;
        var camelCaseBonus = _options.CamelCaseBonus;
        var startOfWordBonus = _options.StartOfWordBonus;
        var pathSeparatorBonus = _options.PathSeparatorBonus;
        var wordSeparatorBonus = _options.WordSeparatorBonus;
        var separatorAlignmentBonus = _options.SeparatorAlignmentBonus;
        var exactSeparatorBonus = _options.ExactSeparatorBonus;
        var skipWordSeparators = _options.SkipWordSeparators;

        // DP buffer: two rows of length tLen
        var bufferSize = tLen * 2;
        int[]? rented = null;

        try
        {
            scoped Span<int> buffer;
            if (bufferSize <= StackallocThresholdChars)
            {
                buffer = stackalloc int[bufferSize];
            }
            else
            {
                rented = ArrayPool<int>.Shared.Rent(bufferSize);
                buffer = rented.AsSpan(0, bufferSize);
            }

            var scores = buffer[..tLen];
            var seqLens = buffer.Slice(tLen, tLen);

            scores.Clear();
            seqLens.Clear();

            ref var scores0 = ref MemoryMarshal.GetReference(scores);
            ref var seqLens0 = ref MemoryMarshal.GetReference(seqLens);
            ref var qRaw0 = ref MemoryMarshal.GetReference(qRaw);
            ref var qFold0 = ref MemoryMarshal.GetReference(qFold);
            ref var tRaw0 = ref MemoryMarshal.GetReference(tRaw);
            ref var tFold0 = ref MemoryMarshal.GetReference(tFold);

            var qiEffective = 0;

            for (var qi = 0; qi < qLen; qi++)
            {
                var qCharFold = Unsafe.Add(ref qFold0, qi);
                var qCharKind = SymbolClassifier.Classify(qCharFold);

                if (skipWordSeparators && qCharKind == SymbolKind.WordSeparator)
                {
                    continue;
                }

                // Hoisted values
                var qRawIsUpper = char.IsUpper(Unsafe.Add(ref qRaw0, qi));

                // row computation
                var leftScore = 0;
                var diagScore = 0;
                var diagSeqLen = 0;

                // limit ti to ensure enough remaining characters to match the rest of the query
                var tiMax = tLen - qEffectiveLen + qiEffective;

                for (var ti = 0; ti <= tiMax; ti++)
                {
                    var upScore = Unsafe.Add(ref scores0, ti);
                    var upSeqLen = Unsafe.Add(ref seqLens0, ti);

                    var charScore = 0;
                    if (diagScore != 0 || qiEffective == 0)
                    {
                        charScore = ComputeCharScore(
                            qi,
                            ti,
                            qCharFold,
                            qCharKind,
                            diagSeqLen,
                            qRawIsUpper,
                            ref tRaw0,
                            ref qFold0,
                            ref tFold0);
                    }

                    var candidateScore = diagScore + charScore;
                    if (charScore != 0 && candidateScore >= leftScore)
                    {
                        Unsafe.Add(ref scores0, ti) = candidateScore;
                        Unsafe.Add(ref seqLens0, ti) = diagSeqLen + 1;
                        leftScore = candidateScore;
                    }
                    else
                    {
                        Unsafe.Add(ref scores0, ti) = leftScore;
                        Unsafe.Add(ref seqLens0, ti) = 0;
                        /* leftScore remains unchanged */
                    }

                    diagScore = upScore;
                    diagSeqLen = upSeqLen;
                }

                // Early exit: no match possible
                if (leftScore == 0)
                {
                    return NoMatchScore;
                }

                // Advance effective query index
                // Only counts non-separator characters if skipWordSeparators is enabled
                qiEffective++;

                if (qiEffective == qEffectiveLen)
                {
                    return leftScore;
                }
            }

            return scores[tLen - 1];

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            int ComputeCharScore(
                int qi,
                int ti,
                char qCharFold,
                SymbolKind qCharKind,
                int seqLen,
                bool qCharRawCurrIsUpper,
                ref char tRaw0,
                ref char qFold0,
                ref char tFold0)
            {
                // Match check:
                // - exact folded char match always ok
                // - otherwise, allow equivalence only for word separators (e.g. '_' matches '-')
                var tCharFold = Unsafe.Add(ref tFold0, ti);
                if (qCharFold != tCharFold)
                {
                    if (!skipWordSeparators)
                    {
                        return 0;
                    }

                    if (qCharKind != SymbolKind.WordSeparator ||
                        SymbolClassifier.Classify(tCharFold) != SymbolKind.WordSeparator)
                    {
                        return 0;
                    }
                }

                // 0. Base char match bonus
                var score = charMatchBonus;

                // 1. Consecutive match bonus
                if (seqLen > 0)
                {
                    score += seqLen * consecutiveMultiplier;
                }

                // 2. Same case bonus
                // Early outs to appease the branch predictor
                if (sameCaseBonus != 0)
                {
                    var tCharRawCurr = Unsafe.Add(ref tRaw0, ti);
                    var tCharRawCurrIsUpper = char.IsUpper(tCharRawCurr);
                    if (qCharRawCurrIsUpper == tCharRawCurrIsUpper)
                    {
                        score += sameCaseBonus;
                    }

                    if (ti == 0)
                    {
                        score += startOfWordBonus;
                        return score;
                    }

                    var tPrevFold = Unsafe.Add(ref tFold0, ti - 1);
                    var tPrevKind = SymbolClassifier.Classify(tPrevFold);
                    if (tPrevKind != SymbolKind.Other)
                    {
                        score += tPrevKind == SymbolKind.PathSeparator
                            ? pathSeparatorBonus
                            : wordSeparatorBonus;

                        if (skipWordSeparators && seqLen == 0 && qi > 0)
                        {
                            var qPrevFold = Unsafe.Add(ref qFold0, qi - 1);
                            var qPrevKind = SymbolClassifier.Classify(qPrevFold);

                            if (qPrevKind == SymbolKind.WordSeparator)
                            {
                                score += separatorAlignmentBonus;

                                if (tPrevKind == SymbolKind.WordSeparator && qPrevFold == tPrevFold)
                                {
                                    score += exactSeparatorBonus;
                                }
                            }
                        }

                        return score;
                    }

                    if (tCharRawCurrIsUpper && seqLen == 0)
                    {
                        score += camelCaseBonus;
                        return score;
                    }

                    return score;
                }
                else
                {
                    if (ti == 0)
                    {
                        score += startOfWordBonus;
                        return score;
                    }

                    var tPrevFold = Unsafe.Add(ref tFold0, ti - 1);
                    var tPrevKind = SymbolClassifier.Classify(tPrevFold);
                    if (tPrevKind != SymbolKind.Other)
                    {
                        score += tPrevKind == SymbolKind.PathSeparator
                            ? pathSeparatorBonus
                            : wordSeparatorBonus;

                        if (skipWordSeparators && seqLen == 0 && qi > 0)
                        {
                            var qPrevFold = Unsafe.Add(ref qFold0, qi - 1);
                            var qPrevKind = SymbolClassifier.Classify(qPrevFold);

                            if (qPrevKind == SymbolKind.WordSeparator)
                            {
                                score += separatorAlignmentBonus;

                                if (tPrevKind == SymbolKind.WordSeparator && qPrevFold == tPrevFold)
                                {
                                    score += exactSeparatorBonus;
                                }
                            }
                        }

                        return score;
                    }

                    if (camelCaseBonus != 0 && seqLen == 0 && char.IsUpper(Unsafe.Add(ref tRaw0, ti)))
                    {
                        score += camelCaseBonus;
                        return score;
                    }

                    return score;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    // Schema ID is for cache invalidation of precomputed targets.
    // Only includes options that affect folding/bloom, not scoring.
    private static uint ComputeSchemaId(PrecomputedFuzzyMatcherOptions o)
    {
        const uint fnvOffset = 2166136261;
        const uint fnvPrime = 16777619;

        var h = fnvOffset;
        h = unchecked((h ^ FolderSchemaVersion) * fnvPrime);
        h = unchecked((h ^ BloomSchemaVersion) * fnvPrime);
        h = unchecked((h ^ (uint)(o.RemoveDiacritics ? 1 : 0)) * fnvPrime);

        return h;
    }
}
