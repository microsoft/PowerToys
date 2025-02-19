// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class StringMatcher
{
    private readonly MatchOption _defaultMatchOption = new();

    public SearchPrecisionScore UserSettingSearchPrecision { get; set; }

    // private readonly IAlphabet _alphabet;
    public StringMatcher(/*IAlphabet alphabet = null*/)
    {
        // _alphabet = alphabet;
    }

    private static StringMatcher? _instance;

    public static StringMatcher Instance
    {
        get
        {
            _instance ??= new StringMatcher();

            return _instance;
        }
        set => _instance = value;
    }

    private static readonly char[] Separator = new[] { ' ' };

    public static MatchResult FuzzySearch(string query, string stringToCompare)
    {
        return Instance.FuzzyMatch(query, stringToCompare);
    }

    public MatchResult FuzzyMatch(string query, string stringToCompare)
    {
        try
        {
            return FuzzyMatch(query, stringToCompare, _defaultMatchOption);
        }
        catch (IndexOutOfRangeException)
        {
            return new MatchResult(false, UserSettingSearchPrecision);
        }
    }

    /// <summary>
    /// Current method:
    /// Character matching + substring matching;
    /// 1. Query search string is split into substrings, separator is whitespace.
    /// 2. Check each query substring's characters against full compare string,
    /// 3. if a character in the substring is matched, loop back to verify the previous character.
    /// 4. If previous character also matches, and is the start of the substring, update list.
    /// 5. Once the previous character is verified, move on to the next character in the query substring.
    /// 6. Move onto the next substring's characters until all substrings are checked.
    /// 7. Consider success and move onto scoring if every char or substring without whitespaces matched
    /// </summary>
    public MatchResult FuzzyMatch(string query, string stringToCompare, MatchOption opt)
    {
        if (string.IsNullOrEmpty(stringToCompare))
        {
            return new MatchResult(false, UserSettingSearchPrecision);
        }

        var bestResult = new MatchResult(false, UserSettingSearchPrecision);

        for (var startIndex = 0; startIndex < stringToCompare.Length; startIndex++)
        {
            MatchResult result = FuzzyMatch(query, stringToCompare, opt, startIndex);
            if (result.Success && (!bestResult.Success || result.Score > bestResult.Score))
            {
                bestResult = result;
            }
        }

        return bestResult;
    }

    private MatchResult FuzzyMatch(string query, string stringToCompare, MatchOption opt, int startIndex)
    {
        if (string.IsNullOrEmpty(stringToCompare) || string.IsNullOrEmpty(query))
        {
            return new MatchResult(false, UserSettingSearchPrecision);
        }

        ArgumentNullException.ThrowIfNull(opt);

        query = query.Trim();

        // if (_alphabet != null)
        // {
        //    query = _alphabet.Translate(query);
        //    stringToCompare = _alphabet.Translate(stringToCompare);
        // }

        // Using InvariantCulture since this is internal
        var fullStringToCompareWithoutCase = opt.IgnoreCase ? stringToCompare.ToUpper(CultureInfo.InvariantCulture) : stringToCompare;
        var queryWithoutCase = opt.IgnoreCase ? query.ToUpper(CultureInfo.InvariantCulture) : query;

        var querySubstrings = queryWithoutCase.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        var currentQuerySubstringIndex = 0;
        var currentQuerySubstring = querySubstrings[currentQuerySubstringIndex];
        var currentQuerySubstringCharacterIndex = 0;

        var firstMatchIndex = -1;
        var firstMatchIndexInWord = -1;
        var lastMatchIndex = 0;
        var allQuerySubstringsMatched = false;
        var matchFoundInPreviousLoop = false;
        var allSubstringsContainedInCompareString = true;

        var indexList = new List<int>();
        List<int> spaceIndices = new List<int>();

        for (var compareStringIndex = startIndex; compareStringIndex < fullStringToCompareWithoutCase.Length; compareStringIndex++)
        {
            // To maintain a list of indices which correspond to spaces in the string to compare
            // To populate the list only for the first query substring
            if (fullStringToCompareWithoutCase[compareStringIndex].Equals(' ') && currentQuerySubstringIndex == 0)
            {
                spaceIndices.Add(compareStringIndex);
            }

            bool compareResult;
            if (opt.IgnoreCase)
            {
                var fullStringToCompare = fullStringToCompareWithoutCase[compareStringIndex].ToString();
                var querySubstring = currentQuerySubstring[currentQuerySubstringCharacterIndex].ToString();
#pragma warning disable CA1309 // Use ordinal string comparison (We are looking for a fuzzy match here)
                compareResult = string.Compare(fullStringToCompare, querySubstring, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) != 0;
#pragma warning restore CA1309 // Use ordinal string comparison
            }
            else
            {
                compareResult = fullStringToCompareWithoutCase[compareStringIndex] != currentQuerySubstring[currentQuerySubstringCharacterIndex];
            }

            if (compareResult)
            {
                matchFoundInPreviousLoop = false;
                continue;
            }

            if (firstMatchIndex < 0)
            {
                // first matched char will become the start of the compared string
                firstMatchIndex = compareStringIndex;
            }

            if (currentQuerySubstringCharacterIndex == 0)
            {
                // first letter of current word
                matchFoundInPreviousLoop = true;
                firstMatchIndexInWord = compareStringIndex;
            }
            else if (!matchFoundInPreviousLoop)
            {
                // we want to verify that there is not a better match if this is not a full word
                // in order to do so we need to verify all previous chars are part of the pattern
                var startIndexToVerify = compareStringIndex - currentQuerySubstringCharacterIndex;

                if (AllPreviousCharsMatched(startIndexToVerify, currentQuerySubstringCharacterIndex, fullStringToCompareWithoutCase, currentQuerySubstring))
                {
                    matchFoundInPreviousLoop = true;

                    // if it's the beginning character of the first query substring that is matched then we need to update start index
                    firstMatchIndex = currentQuerySubstringIndex == 0 ? startIndexToVerify : firstMatchIndex;

                    indexList = GetUpdatedIndexList(startIndexToVerify, currentQuerySubstringCharacterIndex, firstMatchIndexInWord, indexList);
                }
            }

            lastMatchIndex = compareStringIndex + 1;
            indexList.Add(compareStringIndex);

            currentQuerySubstringCharacterIndex++;

            // if finished looping through every character in the current substring
            if (currentQuerySubstringCharacterIndex == currentQuerySubstring.Length)
            {
                // if any of the substrings was not matched then consider as all are not matched
                allSubstringsContainedInCompareString = matchFoundInPreviousLoop && allSubstringsContainedInCompareString;

                currentQuerySubstringIndex++;

                allQuerySubstringsMatched = AllQuerySubstringsMatched(currentQuerySubstringIndex, querySubstrings.Length);
                if (allQuerySubstringsMatched)
                {
                    break;
                }

                // otherwise move to the next query substring
                currentQuerySubstring = querySubstrings[currentQuerySubstringIndex];
                currentQuerySubstringCharacterIndex = 0;
            }
        }

        // proceed to calculate score if every char or substring without whitespaces matched
        if (allQuerySubstringsMatched)
        {
            var nearestSpaceIndex = CalculateClosestSpaceIndex(spaceIndices, firstMatchIndex);
            var score = CalculateSearchScore(query, stringToCompare, firstMatchIndex - nearestSpaceIndex - 1, lastMatchIndex - firstMatchIndex, allSubstringsContainedInCompareString);

            return new MatchResult(true, UserSettingSearchPrecision, indexList, score);
        }

        return new MatchResult(false, UserSettingSearchPrecision);
    }

    // To get the index of the closest space which precedes the first matching index
    private static int CalculateClosestSpaceIndex(List<int> spaceIndices, int firstMatchIndex)
    {
        if (spaceIndices.Count == 0)
        {
            return -1;
        }
        else
        {
            return spaceIndices.OrderBy(item => (firstMatchIndex - item)).Where(item => firstMatchIndex > item).FirstOrDefault(-1);
        }
    }

    private static bool AllPreviousCharsMatched(int startIndexToVerify, int currentQuerySubstringCharacterIndex, string fullStringToCompareWithoutCase, string currentQuerySubstring)
    {
        var allMatch = true;
        for (var indexToCheck = 0; indexToCheck < currentQuerySubstringCharacterIndex; indexToCheck++)
        {
            if (fullStringToCompareWithoutCase[startIndexToVerify + indexToCheck] !=
                currentQuerySubstring[indexToCheck])
            {
                allMatch = false;
            }
        }

        return allMatch;
    }

    private static List<int> GetUpdatedIndexList(int startIndexToVerify, int currentQuerySubstringCharacterIndex, int firstMatchIndexInWord, List<int> indexList)
    {
        var updatedList = new List<int>();

        indexList.RemoveAll(x => x >= firstMatchIndexInWord);

        updatedList.AddRange(indexList);

        for (var indexToCheck = 0; indexToCheck < currentQuerySubstringCharacterIndex; indexToCheck++)
        {
            updatedList.Add(startIndexToVerify + indexToCheck);
        }

        return updatedList;
    }

    private static bool AllQuerySubstringsMatched(int currentQuerySubstringIndex, int querySubstringsLength)
    {
        return currentQuerySubstringIndex >= querySubstringsLength;
    }

    private static int CalculateSearchScore(string query, string stringToCompare, int firstIndex, int matchLen, bool allSubstringsContainedInCompareString)
    {
        // A match found near the beginning of a string is scored more than a match found near the end
        // A match is scored more if the characters in the patterns are closer to each other,
        // while the score is lower if they are more spread out

        // The length of the match is assigned a larger weight factor.
        // I.e. the length is more important than where in the string a match is found.
        const int matchLenWeightFactor = 2;

        var score = 100 * (query.Length + 1) * matchLenWeightFactor / ((1 + firstIndex) + (matchLenWeightFactor * (matchLen + 1)));

        // A match with less characters assigning more weights
        if (stringToCompare.Length - query.Length < 5)
        {
            score += 20;
        }
        else if (stringToCompare.Length - query.Length < 10)
        {
            score += 10;
        }

        if (allSubstringsContainedInCompareString)
        {
            var count = query.Count(c => !char.IsWhiteSpace(c));
            var threshold = 4;
            if (count <= threshold)
            {
                score += count * 10;
            }
            else
            {
                score += (threshold * 10) + ((count - threshold) * 5);
            }
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
}
