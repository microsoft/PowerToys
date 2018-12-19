using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure
{
    public static class StringMatcher
    {
        public static int Score(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                FuzzyMatcher matcher = FuzzyMatcher.Create(target);
                var score = matcher.Evaluate(source).Score;
                return score;
            }
            else
            {
                return 0;
            }
        }

        public static int ScoreForPinyin(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                if(source.Length > 40)
                {
                    Log.Debug($"|Wox.Infrastructure.StringMatcher.ScoreForPinyin|skip too long string: {source}");
                    return 0;
                }
                
                if (Alphabet.ContainsChinese(source))
                {
                    FuzzyMatcher matcher = FuzzyMatcher.Create(target);
                    var combination = Alphabet.PinyinComination(source);
                    var pinyinScore = combination.Select(pinyin => matcher.Evaluate(string.Join("", pinyin)).Score)
                        .Max();
                    var acronymScore = combination.Select(Alphabet.Acronym)
                        .Select(pinyin => matcher.Evaluate(pinyin).Score)
                        .Max();
                    var score = Math.Max(pinyinScore, acronymScore);
                    return score;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        public static bool IsMatch(string source, string target)
        {
            return Score(source, target) > 0;
        }
    }
}
