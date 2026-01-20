// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

using ToolGood.Words.Pinyin;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// Inspired by the fuzzy.rs from edit.exe
public static class FuzzyStringMatcher
{
    private const int NOMATCH = 0;

    /// <summary>
    /// Gets a value indicating whether to support Chinese PinYin.
    /// Automatically enabled when the system UI culture is Simplified Chinese.
    /// </summary>
    public static bool ChinesePinYinSupport { get; } = IsSimplifiedChinese();

    private static bool IsSimplifiedChinese()
    {
        var culture = CultureInfo.CurrentUICulture;

        // Detect Simplified Chinese: zh-CN, zh-Hans, zh-Hans-*
        return culture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
            || culture.Name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
    }

    public static int ScoreFuzzy(string needle, string haystack, bool allowNonContiguousMatches = true)
    {
        var (s, _) = ScoreFuzzyWithPositions(needle, haystack, allowNonContiguousMatches);
        return s;
    }

    public static (int Score, List<int> Positions) ScoreFuzzyWithPositions(string needle, string haystack, bool allowNonContiguousMatches)
        => ScoreAllFuzzyWithPositions(needle, haystack, allowNonContiguousMatches).MaxBy(i => i.Score);

    public static IEnumerable<(int Score, List<int> Positions)> ScoreAllFuzzyWithPositions(string needle, string haystack, bool allowNonContiguousMatches)
    {
        List<string> needles = [needle];
        List<string> haystacks = [haystack];

        if (ChinesePinYinSupport)
        {
            // Remove IME composition split characters.
            var input = needle.Replace("'", string.Empty);
            needles.Add(WordsHelper.GetPinyin(input));
            if (WordsHelper.HasChinese(haystack))
            {
                haystacks.Add(WordsHelper.GetPinyin(haystack));
            }
        }

        return needles.SelectMany(i => haystacks.Select(j => ScoreFuzzyWithPositionsInternal(i, j, allowNonContiguousMatches)));
    }

    private static (int Score, List<int> Positions) ScoreFuzzyWithPositionsInternal(string needle, string haystack, bool allowNonContiguousMatches)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
        {
            return (NOMATCH, new List<int>());
        }

        var target = haystack.ToCharArray();
        var query = needle.ToCharArray();

        if (target.Length < query.Length)
        {
            return (NOMATCH, new List<int>());
        }

        var targetUpper = FoldCase(haystack);
        var queryUpper = FoldCase(needle);
        var targetUpperChars = targetUpper.ToCharArray();
        var queryUpperChars = queryUpper.ToCharArray();

        var area = query.Length * target.Length;
        var scores = new int[area];
        var matches = new int[area];

        for (var qi = 0; qi < query.Length; qi++)
        {
            var qiOffset = qi * target.Length;
            var qiPrevOffset = qi > 0 ? (qi - 1) * target.Length : 0;

            for (var ti = 0; ti < target.Length; ti++)
            {
                var currentIndex = qiOffset + ti;
                var diagIndex = (qi > 0 && ti > 0) ? qiPrevOffset + ti - 1 : 0;
                var leftScore = ti > 0 ? scores[currentIndex - 1] : 0;
                var diagScore = (qi > 0 && ti > 0) ? scores[diagIndex] : 0;
                var matchSeqLen = (qi > 0 && ti > 0) ? matches[diagIndex] : 0;

                var score = (diagScore == 0 && qi != 0) ? 0 :
                    ComputeCharScore(
                        query[qi],
                        queryUpperChars[qi],
                        ti != 0 ? target[ti - 1] : null,
                        target[ti],
                        targetUpperChars[ti],
                        matchSeqLen);

                var isValidScore = score != 0 && diagScore + score >= leftScore &&
                    (allowNonContiguousMatches || qi > 0 ||
                     targetUpperChars.Skip(ti).Take(queryUpperChars.Length).SequenceEqual(queryUpperChars));

                if (isValidScore)
                {
                    matches[currentIndex] = matchSeqLen + 1;
                    scores[currentIndex] = diagScore + score;
                }
                else
                {
                    matches[currentIndex] = NOMATCH;
                    scores[currentIndex] = leftScore;
                }
            }
        }

        var positions = new List<int>();
        if (query.Length > 0 && target.Length > 0)
        {
            var qi = query.Length - 1;
            var ti = target.Length - 1;

            while (true)
            {
                var index = (qi * target.Length) + ti;
                if (matches[index] == NOMATCH)
                {
                    if (ti == 0)
                    {
                        break;
                    }

                    ti--;
                }
                else
                {
                    positions.Add(ti);
                    if (qi == 0 || ti == 0)
                    {
                        break;
                    }

                    qi--;
                    ti--;
                }
            }

            positions.Reverse();
        }

        return (scores[area - 1], positions);
    }

    private static string FoldCase(string input)
    {
        return input.ToUpperInvariant();
    }

    private static int ComputeCharScore(
        char query,
        char queryLower,
        char? targetPrev,
        char targetCurr,
        char targetLower,
        int matchSeqLen)
    {
        if (!ConsiderAsEqual(queryLower, targetLower))
        {
            return 0;
        }

        var score = 1; // Character match bonus

        if (matchSeqLen > 0)
        {
            score += matchSeqLen * 5; // Consecutive match bonus
        }

        if (query == targetCurr)
        {
            score += 1; // Same case bonus
        }

        if (targetPrev.HasValue)
        {
            var sepBonus = ScoreSeparator(targetPrev.Value);
            if (sepBonus > 0)
            {
                score += sepBonus;
            }
            else if (char.IsUpper(targetCurr) && matchSeqLen == 0)
            {
                score += 2; // CamelCase bonus
            }
        }
        else
        {
            score += 8; // Start of word bonus
        }

        return score;
    }

    private static bool ConsiderAsEqual(char a, char b)
    {
        return a == b || (a == '/' && b == '\\') || (a == '\\' && b == '/');
    }

    private static int ScoreSeparator(char ch)
    {
        return ch switch
        {
            '/' or '\\' => 5,
            '_' or '-' or '.' or ' ' or '\'' or '"' or ':' => 4,
            _ => 0,
        };
    }
}
