using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using static Wox.Infrastructure.StringMatcher;

namespace Wox.Infrastructure 
{
    public static class StringMatcher
    {
        public static MatchOption DefaultMatchOption = new MatchOption();

        public static string UserSettingSearchPrecision { get; set; }
        public static bool ShouldUsePinyin { get; set; }

        [Obsolete("This method is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
        public static int Score(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                return FuzzySearch(target, source, DefaultMatchOption).Score;
            }
            else
            {
                return 0;
            }
        }

        [Obsolete("This method is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
        public static bool IsMatch(string source, string target)
        {
            return FuzzySearch(target, source, DefaultMatchOption).Score > 0;
        }

        public static MatchResult FuzzySearch(string query, string stringToCompare)
        {
            return FuzzySearch(query, stringToCompare, DefaultMatchOption);
        }

        /// <summary>
        /// refer to https://github.com/mattyork/fuzzy
        /// </summary>
        public static MatchResult FuzzySearch(string query, string stringToCompare, MatchOption opt)
        {
            if (string.IsNullOrEmpty(stringToCompare) || string.IsNullOrEmpty(query)) return new MatchResult { Success = false };

            query = query.Trim();

            var len = stringToCompare.Length;
            var compareString = opt.IgnoreCase ? stringToCompare.ToLower() : stringToCompare;
            var pattern = opt.IgnoreCase ? query.ToLower() : query;

            var sb = new StringBuilder(stringToCompare.Length + (query.Length * (opt.Prefix.Length + opt.Suffix.Length)));
            var patternIdx = 0;
            var firstMatchIndex = -1;
            var lastMatchIndex = 0;
            char ch;

            var indexList = new List<int>();

            for (var idx = 0; idx < len; idx++)
            {
                ch = stringToCompare[idx];
                if (compareString[idx] == pattern[patternIdx])
                {
                    if (firstMatchIndex < 0)
                        firstMatchIndex = idx;
                    lastMatchIndex = idx + 1;

                    indexList.Add(idx);
                    sb.Append(opt.Prefix + ch + opt.Suffix);
                    patternIdx += 1;
                }
                else
                {
                    sb.Append(ch);
                }

                // match success, append remain char
                if (patternIdx == pattern.Length && (idx + 1) != compareString.Length)
                {
                    sb.Append(stringToCompare.Substring(idx + 1));
                    break;
                }
            }

            // return rendered string if we have a match for every char
            if (patternIdx == pattern.Length)
            {
                var score = CalculateSearchScore(query, stringToCompare, firstMatchIndex, lastMatchIndex - firstMatchIndex);
                var pinyinScore = ScoreForPinyin(stringToCompare, query);

                var result = new MatchResult
                {
                    Success = true,
                    MatchData = indexList,
                    Score = Math.Max(score, pinyinScore)
                };

                return result.Score > 0 ? 
                    result : 
                    new MatchResult { Success = false };
            }

            return new MatchResult { Success = false };
        }

        private static int CalculateSearchScore(string query, string stringToCompare, int firstIndex, int matchLen)
        {
            // A match found near the beginning of a string is scored more than a match found near the end
            // A match is scored more if the characters in the patterns are closer to each other, 
            // while the score is lower if they are more spread out
            var score = 100 * (query.Length + 1) / ((1 + firstIndex) + (matchLen + 1));

            // A match with less characters assigning more weights
            if (stringToCompare.Length - query.Length < 5)
            {
                score += 20;
            }
            else if (stringToCompare.Length - query.Length < 10)
            {
                score += 10;
            }

            return score;
        }

        public enum SearchPrecisionScore
        {
            Regular = 50,
            Low = 20,
            None = 0
        }

        public static int ScoreForPinyin(string source, string target)
        {
            if (!ShouldUsePinyin)
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                if (Alphabet.ContainsChinese(source))
                {
                    var combination = Alphabet.PinyinComination(source);                    
                    var pinyinScore = combination
                        .Select(pinyin => FuzzySearch(target, string.Join("", pinyin)).Score)
                        .Max();
                    var acronymScore = combination.Select(Alphabet.Acronym)                        
                        .Select(pinyin => FuzzySearch(target, pinyin).Score)
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
    }

    public class MatchResult
    {
        public bool Success { get; set; }

        private int _score;
        public int Score
        {
            get
            {
                return _score;
            }
            set
            {
                _score = ApplySearchPrecisionFilter(value);
            }
        }

        /// <summary>
        /// Matched data to highlight.
        /// </summary>
        public List<int> MatchData { get; set; }

        public bool IsSearchPrecisionScoreMet()
        {
            return IsSearchPrecisionScoreMet(Score);
        }

        private bool IsSearchPrecisionScoreMet(int score)
        {
            var precisionScore = (SearchPrecisionScore)Enum.Parse(
               typeof(SearchPrecisionScore),
               UserSettingSearchPrecision ?? SearchPrecisionScore.Regular.ToString());
            return score >= (int)precisionScore;
        }

        private int ApplySearchPrecisionFilter(int score)
        {
            return IsSearchPrecisionScoreMet(score) ? score : 0;
        }
    }

    public class MatchOption
    {
        public MatchOption()
        {
            Prefix = "";
            Suffix = "";
            IgnoreCase = true;
        }

        /// <summary>
        /// prefix of match char, use for hightlight
        /// </summary>
        public string Prefix { get; set; }
        /// <summary>
        /// suffix of match char, use for hightlight
        /// </summary>
        public string Suffix { get; set; }

        public bool IgnoreCase { get; set; }
    }
}
