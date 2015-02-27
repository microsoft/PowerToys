using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Wox.Infrastructure
{
    /// <summary>
    /// refer to https://github.com/mattyork/fuzzy
    /// </summary>
    public class FuzzyMatcher
    {
        private string query;
        private MatchOption opt;

        private FuzzyMatcher(string query, MatchOption opt)
        {
            this.query = query.Trim();
            this.opt = opt;
        }

        public static FuzzyMatcher Create(string query)
        {
            return new FuzzyMatcher(query, new MatchOption());
        }

        public static FuzzyMatcher Create(string query, MatchOption opt)
        {
            return new FuzzyMatcher(query, opt);
        }

        public MatchResult Evaluate(string str)
        {
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(query)) return new MatchResult() { Success = false };

            var len = str.Length;
            var compareString = opt.IgnoreCase ? str.ToLower() : str;
            var pattern = opt.IgnoreCase ? query.ToLower() : query;

            var sb = new StringBuilder(str.Length + (query.Length * (opt.Prefix.Length + opt.Suffix.Length)));
            var patternIdx = 0;
            var firstMatchIndex = -1;
            var lastMatchIndex = 0;
            char ch;
            for (var idx = 0; idx < len; idx++)
            {
                ch = str[idx];
                if (compareString[idx] == pattern[patternIdx])
                {
                    if (firstMatchIndex < 0)
                        firstMatchIndex = idx;
                    lastMatchIndex = idx + 1;

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
                    sb.Append(str.Substring(idx + 1));
                    break;
                }
            }

            // return rendered string if we have a match for every char
            if (patternIdx == pattern.Length)
            {
                return new MatchResult()
                {
                    Success = true,
                    Value = sb.ToString(),
                    Score = CalScore(str, firstMatchIndex, lastMatchIndex - firstMatchIndex)
                };
            }

            return new MatchResult() { Success = false };
        }

        private int CalScore(string str, int firstIndex, int matchLen)
        {
            //a match found near the beginning of a string is scored more than a match found near the end
            //a match is scored more if the characters in the patterns are closer to each other, while the score is lower if they are more spread out
            var score = 100 * (query.Length + 1) / ((1 + firstIndex) + (matchLen + 1));
            //a match with less characters assigning more weights
            if (str.Length - query.Length < 5)
                score = score + 20;
            else if (str.Length - query.Length < 10)
                score = score + 10;

            return score;
        }
    }

    public class MatchResult
    {
        public bool Success { get; set; }
        public int Score { get; set; }
        /// <summary>
        /// hightlight string
        /// </summary>
        public string Value { get; set; }
    }

    public class MatchOption
    {
        public MatchOption()
        {
            this.Prefix = "";
            this.Suffix = "";
            this.IgnoreCase = true;
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
