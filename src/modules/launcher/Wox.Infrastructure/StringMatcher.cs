// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.Plugin.Program.UnitTests")]
[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.System.UnitTests")]
[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Wox.Infrastructure
{
    public class StringMatcher
    {
        private readonly MatchOption _defaultMatchOption = new MatchOption();

        public SearchPrecisionScore UserSettingSearchPrecision { get; set; }

        public static StringMatcher Instance { get; internal set; }

        [Obsolete("This method is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
        public static int Score(string source, string target)
        {
            return FuzzySearch(target, source).Score;
        }

        [Obsolete("This method is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
        public static bool IsMatch(string source, string target)
        {
            return Score(source, target) > 0;
        }

        public static MatchResult FuzzySearch(string query, string stringToCompare)
        {
            return Instance.FuzzyMatch(query, stringToCompare);
        }

        public MatchResult FuzzyMatch(string query, string stringToCompare)
        {
            return FuzzyMatch(query, stringToCompare, _defaultMatchOption);
        }

        /// <summary>
        /// Current method:
        /// Find <paramref name="query"/> as a subsequence of <paramref name="stringToCompare"/>
        /// such that the score is maximized.
        /// </summary>
        public MatchResult FuzzyMatch(string query, string stringToCompare, MatchOption opt)
        {
            if (string.IsNullOrEmpty(stringToCompare) || string.IsNullOrEmpty(query))
            {
                return new MatchResult(false, UserSettingSearchPrecision);
            }

            if (opt == null)
            {
                throw new ArgumentNullException(nameof(opt));
            }

            if (opt.IgnoreCase)
            {
                query = query.ToUpper(CultureInfo.CurrentCulture);
                stringToCompare = stringToCompare.ToUpper(CultureInfo.CurrentCulture);
            }

            int bestMatchScore = -1;
            var bestIndexList = new List<int>();
            for (int startIndex = 0; startIndex <= stringToCompare.Length - query.Length; startIndex++)
            {
                // Optimization: Only start if the first characters match
                if (query[0] != stringToCompare[startIndex])
                {
                    continue;
                }

                int stringIndex = startIndex;
                int queryIndex = 0;

                var indexList = new List<int>();
                while (queryIndex < query.Length && stringIndex < stringToCompare.Length)
                {
                    if (query[queryIndex] == stringToCompare[stringIndex])
                    {
                        indexList.Add(stringIndex);
                        queryIndex++;
                        stringIndex++;
                    }
                    else
                    {
                        stringIndex++;
                    }
                }

                if (queryIndex == query.Length)
                {
                    int matchLen = indexList[indexList.Count - 1] - indexList[0] + 1;
                    int matchScore = CalculateSearchScore(query, stringToCompare, indexList[0], matchLen);
                    if (matchScore > bestMatchScore)
                    {
                        bestMatchScore = matchScore;
                        bestIndexList = indexList;
                    }
                }
            }

            if (bestMatchScore != -1)
            {
                return new MatchResult(true, UserSettingSearchPrecision, bestIndexList, bestMatchScore);
            }

            return new MatchResult(false, UserSettingSearchPrecision);
        }

        private static int CalculateSearchScore(string query, string stringToCompare, int firstIndex, int matchLen)
        {
            // A match found near the beginning of a string is scored more than a match found near the end
            // A match is scored more if the characters in the patterns are closer to each other,
            // while the score is lower if they are more spread out
            var score = 100 * query.Length * 10 / (1 + firstIndex + (10 * matchLen));

            // A match with less characters assigning more weights
            if (stringToCompare.Length - query.Length < 5)
            {
                score += 20;
            }
            else if (stringToCompare.Length - query.Length < 10)
            {
                score += 10;
            }

#pragma warning disable CA1309 // Use ordinal string comparison (Using CurrentCultureIgnoreCase since this relates to queries input by user)
            if (string.Equals(query, stringToCompare, StringComparison.CurrentCultureIgnoreCase))
            {
                var bonusForExactMatch = 10;
                score += bonusForExactMatch;
            }
#pragma warning restore CA1309 // Use ordinal string comparison

            return score;
        }

        public enum SearchPrecisionScore
        {
            Regular = 50,
            Low = 20,
            None = 0,
        }
    }
}
