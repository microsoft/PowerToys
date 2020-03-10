using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hyjiacan.util.p4n;
using hyjiacan.util.p4n.format;
using JetBrains.Annotations;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;

namespace Wox.Infrastructure
{
    public interface IAlphabet
    {
        string Translate(string stringToTranslate);
    }

    public class Alphabet : IAlphabet
    {
        private readonly HanyuPinyinOutputFormat Format = new HanyuPinyinOutputFormat();
        private ConcurrentDictionary<string, string[][]> PinyinCache;
        private BinaryStorage<ConcurrentDictionary<string, string[][]>> _pinyinStorage;
        private Settings _settings;
         
        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializePinyinHelpers();
        }

        private void InitializePinyinHelpers()
        {
            Format.setToneType(HanyuPinyinToneType.WITHOUT_TONE);

            Stopwatch.Normal("|Wox.Infrastructure.Alphabet.Initialize|Preload pinyin cache", () =>
            {
                _pinyinStorage = new BinaryStorage<ConcurrentDictionary<string, string[][]>>("Pinyin");
                PinyinCache = _pinyinStorage.TryLoad(new ConcurrentDictionary<string, string[][]>());

                // force pinyin library static constructor initialize
                PinyinHelper.toHanyuPinyinStringArray('T', Format);
            });
            Log.Info($"|Wox.Infrastructure.Alphabet.Initialize|Number of preload pinyin combination<{PinyinCache.Count}>");
        }

        public string Translate(string str)
        {
            return ConvertChineseCharactersToPinyin(str);
        }

        public string ConvertChineseCharactersToPinyin(string source)
        {
            if (!_settings.ShouldUsePinyin)
                return source;

            if (string.IsNullOrEmpty(source))
                return source;

            if (!ContainsChinese(source))
                return source;
                
            var combination = PinyinCombination(source);
            
            var pinyinArray=combination.Select(x => string.Join("", x));
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
            _pinyinStorage.Save(PinyinCache);
        }

        private static string[] EmptyStringArray = new string[0];
        private static string[][] Empty2DStringArray = new string[0][];

        [Obsolete("Not accurate, eg 音乐 will not return yinyue but returns yinle ")]
        /// <summary>
        /// replace chinese character with pinyin, non chinese character won't be modified
        /// <param name="word"> should be word or sentence, instead of single character. e.g. 微软 </param>
        /// </summary>
        public string[] Pinyin(string word)
        {
            if (!_settings.ShouldUsePinyin)
            {
                return EmptyStringArray;
            }

            var pinyin = word.Select(c =>
            {
                var pinyins = PinyinHelper.toHanyuPinyinStringArray(c);
                var result = pinyins == null ? c.ToString() : pinyins[0];
                return result;
            }).ToArray();
            return pinyin;
        }

        /// <summmary>
        /// replace chinese character with pinyin, non chinese character won't be modified
        /// Because we don't have words dictionary, so we can only return all possiblie pinyin combination
        /// e.g. 音乐 will return yinyue and yinle
        /// <param name="characters"> should be word or sentence, instead of single character. e.g. 微软 </param>
        /// </summmary>
        public string[][] PinyinCombination(string characters)
        {
            if (!_settings.ShouldUsePinyin || string.IsNullOrEmpty(characters))
            {
                return Empty2DStringArray;
            }

            if (!PinyinCache.ContainsKey(characters))
            {
                var allPinyins = new List<string[]>();
                foreach (var c in characters)
                {
                    var pinyins = PinyinHelper.toHanyuPinyinStringArray(c, Format);
                    if (pinyins != null)
                    {
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
                PinyinCache[characters] = combination;
                return combination;
            }
            else
            {
                return PinyinCache[characters];
            }
        }

        public string Acronym(string[] pinyin)
        {
            var acronym = string.Join("", pinyin.Select(p => p[0]));
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
                Log.Debug($"|Wox.Infrastructure.StringMatcher.ScoreForPinyin|skip too long string: {word}");
                return false;
            }

            var chinese = word.Select(PinyinHelper.toHanyuPinyinStringArray)
                              .Any(p => p != null);
            return chinese;
        }

        private string[] Combination(string[] array1, string[] array2)
        {
            if (!_settings.ShouldUsePinyin)
            {
                return EmptyStringArray;
            }

            var combination = (
                from a1 in array1
                from a2 in array2
                select $"{a1};{a2}"
            ).ToArray();
            return combination;
        }
    }
}
