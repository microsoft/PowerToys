// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CmdPal.Common.Text;

public sealed class FuzzyMatcherProvider : IFuzzyMatcherProvider
{
    private readonly IBloomFilter _bloomCalculator = new BloomFilter();
    private readonly IStringFolder _normalizer = new StringFolder();

    private IPrecomputedFuzzyMatcher _current;

    public FuzzyMatcherProvider(PrecomputedFuzzyMatcherOptions core, PinyinFuzzyMatcherOptions? pinyin = null)
    {
        _current = CreateMatcher(core, pinyin);
    }

    public IPrecomputedFuzzyMatcher Current => Volatile.Read(ref _current);

    public void UpdateSettings(PrecomputedFuzzyMatcherOptions core, PinyinFuzzyMatcherOptions? pinyin = null)
    {
        Volatile.Write(ref _current, CreateMatcher(core, pinyin));
    }

    private IPrecomputedFuzzyMatcher CreateMatcher(PrecomputedFuzzyMatcherOptions core, PinyinFuzzyMatcherOptions? pinyin)
    {
        return pinyin is null || !IsPinyinEnabled(pinyin)
            ? new PrecomputedFuzzyMatcher(core, _normalizer, _bloomCalculator)
            : new PrecomputedFuzzyMatcherWithPinyin(core, pinyin, _normalizer, _bloomCalculator);
    }

    private static bool IsPinyinEnabled(PinyinFuzzyMatcherOptions o)
    {
        return o.Mode switch
        {
            PinyinMode.Off => false,
            PinyinMode.On => true,
            PinyinMode.AutoSimplifiedChineseUi => IsSimplifiedChineseUi(),
            _ => false,
        };
    }

    private static bool IsSimplifiedChineseUi()
    {
        var culture = CultureInfo.CurrentUICulture;
        return culture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
               || culture.Name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
    }
}
