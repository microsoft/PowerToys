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
    private const int StackallocTargetLenThreshold = 512;

    private static readonly SearchValues<char> AllSeparators = SearchValues.Create("/\\_- '\":.");
    private readonly IBloomCalculator _bloom;
    private readonly INormalizer _normalizer;

    private readonly PrecomputedFuzzyMatcherOptions _options;

    public PrecomputedFuzzyMatcher(
        PrecomputedFuzzyMatcherOptions? options = null,
        INormalizer? normalization = null,
        IBloomCalculator? bloomCalculator = null)
    {
        _options = options ?? PrecomputedFuzzyMatcherOptions.Default;
        _bloom = bloomCalculator ?? new BloomCalculator();
        _normalizer = normalization ?? new Normalizer();

        SchemaId = ComputeSchemaId(_options);
    }

    public uint SchemaId { get; }

    public FuzzyQuery PrecomputeQuery(string? input) => PrecomputeQuery(input, null);

    public FuzzyTarget PrecomputeTarget(string? input) => PrecomputeTarget(input, null);

    public int Score(scoped in FuzzyQuery query, scoped in FuzzyTarget target)
    {
        return ScoreFieldNonContiguous(query, target, true);
    }

    private FuzzyQuery PrecomputeQuery(string? input, string? secondaryInput)
    {
        input ??= string.Empty;

        var normalized = _normalizer.Normalize(input);
        var folded = _normalizer.FoldCase(normalized);
        var normalizedNoSep = RemoveSeparators(normalized, out var hasSeparators);
        var foldedNoSep = hasSeparators ? RemoveSeparators(folded, out _) : folded;
        var bloom = _bloom.ComputeBloomFilter(foldedNoSep);

        string? secondaryNormalized = null;
        string? secondaryFolded = null;
        ulong secondaryBloom = 0;

        if (!string.IsNullOrEmpty(secondaryInput))
        {
            secondaryNormalized = _normalizer.Normalize(secondaryInput);
            secondaryFolded = _normalizer.FoldCase(secondaryNormalized);
            secondaryBloom = _bloom.ComputeBloomFilter(secondaryFolded);
        }

        return new FuzzyQuery(
            input,
            normalized,
            folded,
            normalizedNoSep,
            foldedNoSep,
            hasSeparators,
            bloom,
            secondaryNormalized,
            secondaryFolded,
            secondaryBloom);
    }

    private FuzzyTarget PrecomputeTarget(string? input, string? secondaryInput)
    {
        input ??= string.Empty;

        var normalized = _normalizer.Normalize(input);
        var folded = _normalizer.FoldCase(normalized);
        var bloom = _bloom.ComputeBloomFilter(folded);

        string? secondaryNormalized = null;
        string? secondaryFolded = null;
        ulong secondaryBloom = 0;

        if (!string.IsNullOrEmpty(secondaryInput))
        {
            secondaryNormalized = _normalizer.Normalize(secondaryInput);
            secondaryFolded = _normalizer.FoldCase(secondaryNormalized);
            secondaryBloom = _bloom.ComputeBloomFilter(secondaryFolded);
        }

        return new FuzzyTarget(
            normalized,
            folded,
            bloom,
            secondaryNormalized,
            secondaryFolded,
            secondaryBloom);
    }

    private static string RemoveSeparators(string input, out bool removedAny)
    {
        removedAny = false;
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var src = input.AsSpan();
        var firstSepIndex = src.IndexOfAny(AllSeparators);
        if (firstSepIndex < 0)
        {
            return input;
        }

        removedAny = true;

        const int StackAllocLimit = 2048;

        char[]? rented = null;
        var destination =
            input.Length <= StackAllocLimit
                ? stackalloc char[input.Length]
                : rented = ArrayPool<char>.Shared.Rent(input.Length);

        // If we rented, the span may be larger than input.Length. Limit it.
        destination = destination[..input.Length];

        try
        {
            // 1. Copy the first clean chunk immediately
            src[..firstSepIndex].CopyTo(destination);
            var destIndex = firstSepIndex;

            // 2. Scan the rest in chunks
            var remaining = src[(firstSepIndex + 1)..];

            while (!remaining.IsEmpty)
            {
                var nextSep = remaining.IndexOfAny(AllSeparators);

                if (nextSep < 0)
                {
                    remaining.CopyTo(destination[destIndex..]);
                    destIndex += remaining.Length;
                    break;
                }

                if (nextSep > 0)
                {
                    remaining[..nextSep].CopyTo(destination[destIndex..]);
                    destIndex += nextSep;
                }

                remaining = remaining[(nextSep + 1)..];
            }

            return new string(destination[..destIndex]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int ScoreFieldNonContiguous(in FuzzyQuery query, in FuzzyTarget target, bool ignoreQuerySeparatorsForDp)
    {
        var qNorm = ignoreQuerySeparatorsForDp ? query.NormalizedNoSepSpan : query.NormalizedSpan;
        var qFold = ignoreQuerySeparatorsForDp ? query.FoldedNoSepSpan : query.FoldedSpan;

        var qLen = qNorm.Length;
        var tLen = target.Length;

        if (qLen == 0 || tLen == 0)
        {
            return NoMatchScore;
        }

        var bestScore = 0;

        // 1. Primary → Primary
        if (tLen >= qLen && _bloom.MightContain(target.Bloom, query.Bloom))
        {
            bestScore = ScoreDpNonContiguous(qNorm, qFold, target.NormalizedSpan, target.FoldedSpan);
        }

        // 2. Secondary → Secondary (if both have)
        if (query.HasSecondary && target.HasSecondary)
        {
            var qSecNorm = query.SecondaryNormalizedSpan;
            var qSecFold = query.SecondaryFoldedSpan;

            if (target.SecondaryLength >= qSecNorm.Length &&
                _bloom.MightContain(target.SecondaryBloom, query.SecondaryBloom))
            {
                var score = ScoreDpNonContiguous(qSecNorm, qSecFold, target.SecondaryNormalizedSpan, target.SecondaryFoldedSpan);
                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        // 3. Primary query → Secondary target
        if (target.HasSecondary && target.SecondaryLength >= qLen &&
            _bloom.MightContain(target.SecondaryBloom, query.Bloom))
        {
            var score = ScoreDpNonContiguous(qNorm, qFold, target.SecondaryNormalizedSpan, target.SecondaryFoldedSpan);
            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        // 4. Secondary query → Primary target
        if (query.HasSecondary)
        {
            var qSecNorm = query.SecondaryNormalizedSpan;
            var qSecFold = query.SecondaryFoldedSpan;

            if (tLen >= qSecNorm.Length && _bloom.MightContain(target.Bloom, query.SecondaryBloom))
            {
                var score = ScoreDpNonContiguous(qSecNorm, qSecFold, target.NormalizedSpan, target.FoldedSpan);
                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        return bestScore;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        [SkipLocalsInit]
        int ScoreDpNonContiguous(
            scoped in ReadOnlySpan<char> qNorm,
            scoped in ReadOnlySpan<char> qFold,
            scoped in ReadOnlySpan<char> tNorm,
            scoped in ReadOnlySpan<char> tFold)
        {
            Debug.Assert(qNorm.Length == qFold.Length, "Normalized and folder spans are traversed in a lockstep: requires qNorm.Length == qFold.Length");
            Debug.Assert(tNorm.Length == tFold.Length, "Normalized and folder spans are traversed in a lockstep: requires tNorm.Length == tFold.Length");

            var tLen = tNorm.Length;

            var charMatchBonus = _options.CharMatchBonus;
            var sameCaseBonus = _options.SameCaseBonus;
            var consecutiveMultiplier = _options.ConsecutiveMultiplier;
            var camelCaseBonus = _options.CamelCaseBonus;
            var startOfWordBonus = _options.StartOfWordBonus;
            var pathSeparatorBonus = _options.PathSeparatorBonus;
            var otherSeparatorBonus = _options.OtherSeparatorBonus;

            var bufferSize = tLen * 2;

            if (tLen <= StackallocTargetLenThreshold)
            {
                Span<int> buf = stackalloc int[bufferSize];
                return ScoreDpWithBuffers(buf, in qNorm, in qFold, in tNorm, in tFold);
            }

            var rented = ArrayPool<int>.Shared.Rent(bufferSize);
            try
            {
                return ScoreDpWithBuffers(rented.AsSpan(0, bufferSize), in qNorm, in qFold, in tNorm, in tFold);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rented);
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            [SkipLocalsInit]
            int ScoreDpWithBuffers(
                Span<int> buf,
                scoped in ReadOnlySpan<char> qNorm,
                scoped in ReadOnlySpan<char> qFold,
                scoped in ReadOnlySpan<char> tNorm,
                scoped in ReadOnlySpan<char> tFold)
            {
                var scores = buf[..tLen];
                var seqLens = buf.Slice(tLen, tLen);

                scores.Clear();
                seqLens.Clear();

                ref var scores0 = ref MemoryMarshal.GetReference(scores);
                ref var seqLens0 = ref MemoryMarshal.GetReference(seqLens);
                ref var tNorm0 = ref MemoryMarshal.GetReference(tNorm);
                ref var tFold0 = ref MemoryMarshal.GetReference(tFold);

                for (var qi = 0; qi < qNorm.Length; qi++)
                {
                    var qn = qNorm[qi];
                    var qf = qFold[qi];

                    var leftScore = 0;
                    var diagScore = 0;
                    var diagSeqLen = 0;

                    for (var ti = 0; ti < tLen; ti++)
                    {
                        var nextDiagScore = Unsafe.Add(ref scores0, ti);
                        var nextDiagSeqLen = Unsafe.Add(ref seqLens0, ti);

                        var tcNorm = Unsafe.Add(ref tNorm0, ti);
                        var tcFold = Unsafe.Add(ref tFold0, ti);

                        var charScore = 0;
                        if (diagScore != 0 || qi == 0)
                        {
                            charScore = ComputeCharScore(qn, qf, ti, tcNorm, tcFold, diagSeqLen, ref tNorm0);
                        }

                        var diagPlus = diagScore + charScore;

                        if (charScore != 0 && diagPlus >= leftScore)
                        {
                            Unsafe.Add(ref seqLens0, ti) = diagSeqLen + 1;
                            Unsafe.Add(ref scores0, ti) = diagPlus;
                        }
                        else
                        {
                            Unsafe.Add(ref seqLens0, ti) = 0;
                            Unsafe.Add(ref scores0, ti) = leftScore;
                        }

                        leftScore = Unsafe.Add(ref scores0, ti);
                        diagScore = nextDiagScore;
                        diagSeqLen = nextDiagSeqLen;
                    }

                    if (leftScore == 0)
                    {
                        return 0;
                    }
                }

                return scores[tLen - 1];

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                int ComputeCharScore(char qn, char qf, int ti, char tcNorm, char tcFold, int seqLen, ref char tNorm0)
                {
                    if (!ConsiderAsEqual(qf, tcFold))
                    {
                        return 0;
                    }

                    var score = charMatchBonus;

                    if (seqLen > 0)
                    {
                        score += seqLen * consecutiveMultiplier;
                    }

                    if (qn == tcNorm)
                    {
                        score += sameCaseBonus;
                    }

                    if (ti == 0)
                    {
                        score += startOfWordBonus;
                        return score;
                    }

                    var prevChar = Unsafe.Add(ref tNorm0, ti - 1);
                    var sepBonus = ScoreSeparator(prevChar);

                    if (sepBonus > 0)
                    {
                        score += sepBonus;
                    }
                    else if (char.IsUpper(tcNorm) && seqLen == 0)
                    {
                        score += camelCaseBonus;
                    }

                    return score;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int ScoreSeparator(char ch)
            {
                return ch switch
                {
                    '/' or '\\' => pathSeparatorBonus,
                    '_' or '-' or '.' or ' ' or '\'' or '"' or ':' => otherSeparatorBonus,
                    _ => 0,
                };
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ConsiderAsEqual(char a, char b) => a == b || (a == '/' && b == '\\') || (a == '\\' && b == '/');

    private static uint ComputeSchemaId(PrecomputedFuzzyMatcherOptions o)
    {
        const uint fnvOffset = 2166136261;
        const uint fnvPrime = 16777619;

        var h = fnvOffset;
        h = AddInt(h, o.StartOfWordBonus);
        h = AddInt(h, o.PathSeparatorBonus);
        h = AddInt(h, o.OtherSeparatorBonus);
        h = AddInt(h, o.CamelCaseBonus);
        h = AddInt(h, o.SameCaseBonus);
        h = AddInt(h, o.CharMatchBonus);
        h = AddInt(h, o.ConsecutiveMultiplier);
        return h;

        static uint AddInt(uint h, int v)
        {
            return unchecked((h ^ (uint)v) * fnvPrime);
        }

        /*
        static uint AddBool(uint h, bool v)
        {
            return unchecked((h ^ (v ? 1u : 0u)) * fnvPrime);
        }
        */
    }
}
