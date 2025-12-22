// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;
using ToolGood.Words.Pinyin;

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class PrecomputedFuzzyMatcherWithPinyin : IPrecomputedFuzzyMatcher
{
    private readonly IBloomCalculator _bloom;
    private readonly PrecomputedFuzzyMatcher _core;

    private readonly INormalizer _normalizer;
    private readonly PinyinFuzzyMatcherOptions _pinyin;

    public PrecomputedFuzzyMatcherWithPinyin(
        PrecomputedFuzzyMatcherOptions coreOptions,
        PinyinFuzzyMatcherOptions pinyinOptions,
        INormalizer normalizer,
        IBloomCalculator bloom)
    {
        _pinyin = pinyinOptions;
        _normalizer = normalizer;
        _bloom = bloom;

        _core = new PrecomputedFuzzyMatcher(coreOptions, normalizer, bloom);

        SchemaId = CombineSchema(_core.SchemaId, _pinyin);
    }

    public uint SchemaId { get; }

    public FuzzyQuery PrecomputeQuery(string? input)
    {
        input ??= string.Empty;

        var primary = _core.PrecomputeQuery(input);

        // Fast exit if effectively off (provider should already filter, but keep robust)
        if (!IsPinyinEnabled(_pinyin))
        {
            return primary;
        }

        // Match legacy: remove apostrophes for query secondary
        var queryForPinyin = _pinyin.RemoveApostrophesForQuery ? RemoveApostrophesIfAny(input) : input;

        var pinyin = WordsHelper.GetPinyin(queryForPinyin);
        if (string.IsNullOrEmpty(pinyin))
        {
            return primary;
        }

        var secNorm = _normalizer.Normalize(pinyin);
        var secFold = _normalizer.FoldCase(secNorm);
        var secBloom = _bloom.ComputeBloomFilter(secFold);

        return new FuzzyQuery(
            primary.Text,
            primary.Normalized,
            primary.Folded,
            primary.NormalizedNoSep,
            primary.FoldedNoSep,
            primary.HasSeparators,
            primary.Bloom,
            secNorm,
            secFold,
            secBloom);
    }

    public FuzzyTarget PrecomputeTarget(string? input)
    {
        input ??= string.Empty;

        var primary = _core.PrecomputeTarget(input);

        if (!IsPinyinEnabled(_pinyin))
        {
            return primary;
        }

        // Match legacy: only compute target pinyin when target contains Chinese
        if (!ContainsToolGoodChinese(input))
        {
            return primary;
        }

        var pinyin = WordsHelper.GetPinyin(input);
        if (string.IsNullOrEmpty(pinyin))
        {
            return primary;
        }

        var secNorm = _normalizer.Normalize(pinyin);
        var secFold = _normalizer.FoldCase(secNorm);
        var secBloom = _bloom.ComputeBloomFilter(secFold);

        return new FuzzyTarget(
            primary.Normalized,
            primary.Folded,
            primary.Bloom,
            secNorm,
            secFold,
            secBloom);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Score(scoped in FuzzyQuery query, scoped in FuzzyTarget target)
        => _core.Score(in query, in target);

    private static bool IsPinyinEnabled(PinyinFuzzyMatcherOptions o) => o.Mode switch
    {
        PinyinMode.Off => false,
        PinyinMode.On => true,
        PinyinMode.AutoSimplifiedChineseUi => IsSimplifiedChineseUi(),
        _ => false,
    };

    private static bool IsSimplifiedChineseUi()
    {
        var culture = CultureInfo.CurrentUICulture;
        return culture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
               || culture.Name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsToolGoodChinese(string s)
    {
        return WordsHelper.HasChinese(s);
    }

    private static string RemoveApostrophesIfAny(string input)
    {
        var first = input.IndexOf('\'');
        if (first < 0)
        {
            return input;
        }

        var removeCount = 1;
        for (var i = first + 1; i < input.Length; i++)
        {
            if (input[i] == '\'')
            {
                removeCount++;
            }
        }

        return string.Create(input.Length - removeCount, input, static (dst, src) =>
        {
            var di = 0;
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c == '\'')
                {
                    continue;
                }

                dst[di++] = c;
            }
        });
    }

    private static uint CombineSchema(uint coreSchemaId, PinyinFuzzyMatcherOptions p)
    {
        const uint fnvOffset = 2166136261;
        const uint fnvPrime = 16777619;

        var h = fnvOffset;
        h = unchecked((h ^ coreSchemaId) * fnvPrime);
        h = unchecked((h ^ (uint)p.Mode) * fnvPrime);
        h = unchecked((h ^ (p.RemoveApostrophesForQuery ? 1u : 0u)) * fnvPrime);

        // bump if you change formatting/conversion behavior
        const uint pinyinAlgoVersion = 1;
        h = unchecked((h ^ pinyinAlgoVersion) * fnvPrime);

        return h;
    }
}
