using System;
using System.Collections.Generic;
using System.Linq;
using hyjiacan.util.p4n;
using hyjiacan.util.p4n.format;

namespace Wox.Infrastructure
{
    public static class Alphabet
    {
        public static HanyuPinyinOutputFormat format = new HanyuPinyinOutputFormat();

        static Alphabet()
        {
            format.setToneType(HanyuPinyinToneType.WITHOUT_TONE);
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
        public static IEnumerable<string[]> PinyinComination(string characters)
        {
            var allPinyins = new List<string[]>();
            foreach (var c in characters)
            {
                var pinyins = PinyinHelper.toHanyuPinyinStringArray(c, format);
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

            var comination = allPinyins.Aggregate(Combination).Select(c => c.Split(';'));

            return comination;
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
