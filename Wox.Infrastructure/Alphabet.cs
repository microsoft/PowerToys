using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using hyjiacan.util.p4n;
using hyjiacan.util.p4n.format;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;

namespace Wox.Infrastructure
{
    public static class Alphabet
    {
        private static readonly HanyuPinyinOutputFormat Format = new HanyuPinyinOutputFormat();
        private static ConcurrentDictionary<string, string[][]> PinyinCache;
        private static BinaryStorage<ConcurrentDictionary<string, string[][]>> _pinyinStorage;

        public static void Initialize()
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

        public static void Save()
        {
            _pinyinStorage.Save(PinyinCache);
        }

        /// <summary>
        /// replace chinese character with pinyin, non chinese character won't be modified
        /// <param name="word"> should be word or sentence, instead of single character. e.g. 微软 </param>
        /// </summary>
        public static string[] Pinyin(string word)
        {
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
        public static string[][] PinyinComination(string characters)
        {
            if (!string.IsNullOrEmpty(characters))
            {
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
            else
            {
                return new string[][] { };
            }
        }

        public static string Acronym(string[] pinyin)
        {
            var acronym = string.Join("", pinyin.Select(p => p[0]));
            return acronym;
        }

        public static bool ContainsChinese(string word)
        {
            var chinese = word.Select(PinyinHelper.toHanyuPinyinStringArray)
                              .Any(p => p != null);
            return chinese;
        }

        private static string[] Combination(string[] array1, string[] array2)
        {
            var combination = (
                from a1 in array1
                from a2 in array2
                select $"{a1};{a2}"
            ).ToArray();
            return combination;
        }


    }
}
