// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Class housing fuzzy matching methods
    /// </summary>
    internal static class FuzzyMatching
    {
        /// <summary>
        /// Find the best match (the one with the smallest span) using a Dynamic Programming approach
        /// to minimize candidate matches.
        /// </summary>
        /// <param name="text">The text to search inside of.</param>
        /// <param name="searchText">The text to search for.</param>
        /// <returns>The index location of each of the letters in the best match.</returns>
        internal static List<int> FindBestFuzzyMatch(string text, string searchText)
        {
            ArgumentNullException.ThrowIfNull(searchText);

            ArgumentNullException.ThrowIfNull(text);

            var sLower = searchText.ToLower(CultureInfo.CurrentCulture);
            var tLower = text.ToLower(CultureInfo.CurrentCulture);
            int m = sLower.Length;
            int n = tLower.Length;

            // A subsequence longer than the candidate text can never match.
            if (m > n)
            {
                return [];
            }

            // bestStart[k, i] stores the latest possible start index of a match for s[0..k] that
            // ends exactly at t[i], or -1 if no such match exists.
            //
            // Tracking the latest start ensures that we only retain the smallest span of all matches
            // that end at i.
            int[,] bestStart = new int[m, n];

            // parent[k, i] stores the index where the previous character matched to allow for
            // reconstruction of the best path once the DP step completes.
            int[,] parent = new int[m, n];

            // Initialize tables.
            for (int k = 0; k < m; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    bestStart[k, i] = -1;
                }
            }

            // Base case: match the first character of the search string s[0].
            for (int i = 0; i < n; i++)
            {
                if (tLower[i] == sLower[0])
                {
                    bestStart[0, i] = i;
                    parent[0, i] = -1;
                }
            }

            // Dynamic programming step: extend matches for the remaining characters s[1..m-1].
            for (int k = 1; k < m; k++)
            {
                int currentMaxStart = -1;
                int currentParentIndex = -1;

                for (int i = 0; i < n; i++)
                {
                    // 1. Try to match s[k] at t[i].
                    // We must use a valid start from the previous row (k-1) that appeared BEFORE i.
                    // 'currentMaxStart' holds the best start value from indices 0 to i-1.
                    if (tLower[i] == sLower[k])
                    {
                        if (currentMaxStart != -1)
                        {
                            bestStart[k, i] = currentMaxStart;
                            parent[k, i] = currentParentIndex;
                        }
                    }

                    // 2. Maintain the dominating predecessor for the next column.
                    // We only keep the match with the latest start index, as it strictly dominates
                    // all earlier-starting matches for the purpose of minimizing the match span.
                    if (bestStart[k - 1, i] > currentMaxStart)
                    {
                        currentMaxStart = bestStart[k - 1, i];
                        currentParentIndex = i;
                    }
                }
            }

            // Select the ending position that minimizes span.
            int bestEndIndex = -1;
            int maxScore = int.MinValue;

            // Score logic: -(LastIndex - StartIndex).
            // We want to Maximize Score => Minimize Span.
            for (int i = 0; i < n; i++)
            {
                if (bestStart[m - 1, i] != -1)
                {
                    int start = bestStart[m - 1, i];
                    int score = -(i - start);

                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestEndIndex = i;
                    }
                }
            }

            if (bestEndIndex == -1)
            {
                return [];
            }

            // Reconstruct only the winning path.
            var result = new List<int>(m);
            int curr = bestEndIndex;

            for (int k = m - 1; k >= 0; k--)
            {
                result.Add(curr);
                curr = parent[k, curr];
            }

            result.Reverse();
            return result;
        }

        /// <summary>
        /// Calculates the score for a string
        /// </summary>
        /// <param name="matches">the index of the matches</param>
        /// <returns>an integer representing the score</returns>
        internal static int CalculateScoreForMatches(List<int> matches)
        {
            ArgumentNullException.ThrowIfNull(matches);

            var score = 0;

            for (int currentIndex = 1; currentIndex < matches.Count; currentIndex++)
            {
                var previousIndex = currentIndex - 1;

                score -= matches[currentIndex] - matches[previousIndex];
            }

            return score == 0 ? -10000 : score;
        }
    }
}
