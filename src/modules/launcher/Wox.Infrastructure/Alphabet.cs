// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using hyjiacan.py4n;

using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure;

public class Alphabet : IAlphabet
{
    private readonly PinyinFormat _pinyinFormat =
        PinyinFormat.CAPITALIZE_FIRST_LETTER |
        PinyinFormat.WITH_V |
        PinyinFormat.WITHOUT_TONE;

    private ConcurrentDictionary<string, string[][]> _pinyinCache;
    private WoxJsonStorage<ConcurrentDictionary<string, string[][]>> _pinyinStorage;
    private PowerToysRunSettings _settings;

    public void Initialize(PowerToysRunSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializePinyinHelpers();
    }

    private void InitializePinyinHelpers()
    {
        Stopwatch.Normal("|Wox.Infrastructure.Alphabet.Initialize|Preload pinyin cache", () =>
        {
            _pinyinStorage = new WoxJsonStorage<ConcurrentDictionary<string, string[][]>>("Pinyin");
            _pinyinCache = _pinyinStorage.Load();

            // force pinyin library static constructor initialize
            Pinyin4Net.GetPinyin('一', _pinyinFormat);
        });
        Log.Info($"Number of preload pinyin combination<{_pinyinCache.Count}>", GetType());
    }

    public string Translate(string stringToTranslate)
    {
        return ConvertChineseCharactersToPinyin(stringToTranslate);
    }

    public string ConvertChineseCharactersToPinyin(string source)
    {
        if (!_settings.ShouldUsePinyin)
        {
            return source;
        }

        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!ContainsChinese(source))
        {
            return source;
        }

        var combination = PinyinCombination(source);

        var pinyinArray = combination.Select(x => string.Join(string.Empty, x));
        var acronymArray = combination.Select(Acronym).Distinct();

        var joinedSingleStringCombination = new StringBuilder();
        var all = acronymArray.Concat(pinyinArray);
        all.ToList().ForEach(x => joinedSingleStringCombination.Append(x));

        return joinedSingleStringCombination.ToString();
    }

    public void Save()
    {
        if (!_settings.ShouldUsePinyin)
        {
            return;
        }

        GetPinyinCacheAsDictionary();
        _pinyinStorage.Save();
    }

    private static readonly string[] _emptyStringArray = Array.Empty<string>();
    private static readonly string[][] _empty2DStringArray = Array.Empty<string[]>();

    /// <summary>
    /// replace chinese character with pinyin, non chinese character won't be modified
    /// <param name="word"> should be word or sentence, instead of single character. e.g. 微软 </param>
    /// </summary>
    [Obsolete("Not accurate, eg 音乐 will not return yinyue but returns yinle ")]
    public string[] Pinyin(string word)
    {
        if (!_settings.ShouldUsePinyin)
        {
            return _emptyStringArray;
        }

        var pinyin = word.Select(c =>
        {
            string result = c.ToString();
            if (PinyinUtil.IsHanzi(c))
            {
                var pinyins = Pinyin4Net.GetPinyin(c);
                result = pinyins[0];
            }

            return result;
        }).ToArray();
        return pinyin;
    }

    /// <summary>
    /// replace chinese character with pinyin, non chinese character won't be modified
    /// Because we don't have words dictionary, so we can only return all possibly pinyin combination
    /// e.g. 音乐 will return yinyue and yinle
    /// <param name="characters"> should be word or sentence, instead of single character. e.g. 微软 </param>
    /// </summary>
    public string[][] PinyinCombination(string characters)
    {
        if (!_settings.ShouldUsePinyin || string.IsNullOrEmpty(characters))
        {
            return _empty2DStringArray;
        }

        if (!_pinyinCache.TryGetValue(characters, out string[][] value))
        {
            var allPinyins = new List<string[]>();
            foreach (var c in characters)
            {
                if (PinyinUtil.IsHanzi(c))
                {
                    var pinyins = Pinyin4Net.GetPinyin(c, _pinyinFormat);
                    var r = pinyins.Distinct().ToArray();
                    allPinyins.Add(r);
                }
                else
                {
                    var r = new[] { c.ToString() };
                    allPinyins.Add(r);
                }
            }

            var combination = allPinyins.Aggregate(Combination).Select(c => c.Split(';')).ToArray();
            _pinyinCache[characters] = combination;
            return combination;
        }
        else
        {
            return value;
        }
    }

    public string Acronym(string[] pinyin)
    {
        var acronym = string.Join(string.Empty, pinyin.Select(p => p[0]));
        return acronym;
    }

    public bool ContainsChinese(string word)
    {
        if (!_settings.ShouldUsePinyin)
        {
            return false;
        }

        if (word.Length > 40)
        {
            // Skip strings that are too long string for Pinyin conversion.
            return false;
        }

        var chinese = word.Any(PinyinUtil.IsHanzi);
        return chinese;
    }

    private string[] Combination(string[] array1, string[] array2)
    {
        if (!_settings.ShouldUsePinyin)
        {
            return _emptyStringArray;
        }

        var combination = (
            from a1 in array1
            from a2 in array2
            select $"{a1};{a2}"
        ).ToArray();
        return combination;
    }

    private Dictionary<string, string[][]> GetPinyinCacheAsDictionary()
    {
        return new Dictionary<string, string[][]>(_pinyinCache);
    }
}
