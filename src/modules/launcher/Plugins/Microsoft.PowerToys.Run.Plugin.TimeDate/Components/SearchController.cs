// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    /// <summary>
    /// SearchController: Class tot hold the search method that filter available date time formats
    /// Extra class to simplify code in <see cref="Main"/> class
    /// </summary>
    internal static class SearchController
    {
        /// <summary>
        /// Var that holds the delimiter between format and date
        /// </summary>
        private const string InputDelimiter = "::";

        /// <summary>
        /// A list of conjunctions that we ignore on search
        /// </summary>
        private static readonly string[] _conjunctionList = Resources.Microsoft_plugin_timedate_Search_ConjunctionList.Split("; ");

        /// <summary>
        /// Searches for results
        /// </summary>
        /// <param name="query">Search query object</param>
        /// <returns>List of Wox <see cref="Result"/>s.</returns>
        internal static List<Result> ExecuteSearch(Query query, string iconTheme)
        {
            List<AvailableResult> availableFormats = new List<AvailableResult>();
            List<Result> results = new List<Result>();
            bool isKeywordSearch = !string.IsNullOrEmpty(query.ActionKeyword);
            bool isEmptySearchInput = string.IsNullOrWhiteSpace(query.Search);
            string searchTerm = query.Search;

            // Last input parsing error
            string lastInputParsingErrorReason = string.Empty;

            // Conjunction search without keyword => return no results
            // (This improves the results on global queries.)
            if (!isKeywordSearch && _conjunctionList.Any(x => x.Equals(searchTerm, StringComparison.CurrentCultureIgnoreCase)))
            {
                return results;
            }

            // Switch search type
            if (isEmptySearchInput || (!isKeywordSearch && TimeDateSettings.Instance.OnlyDateTimeNowGlobal))
            {
                // Return all results for system time/date on empty keyword search
                // or only time, date and now results for system time on global queries if the corresponding setting is enabled
                availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch));
            }
            else if (Regex.IsMatch(searchTerm, @".+" + Regex.Escape(InputDelimiter) + @".+"))
            {
                // Search for specified format with specified time/date value
                var userInput = searchTerm.Split(InputDelimiter);
                if (TimeAndDateHelper.ParseStringAsDateTime(userInput[1], out DateTime timestamp, out lastInputParsingErrorReason))
                {
                    availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, null, null, timestamp));
                    searchTerm = userInput[0];
                }
            }
            else if (TimeAndDateHelper.ParseStringAsDateTime(searchTerm, out DateTime timestamp, out lastInputParsingErrorReason))
            {
                // Return all formats for specified time/date value
                availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, null, null, timestamp));
                searchTerm = string.Empty;
            }
            else
            {
                // Search for specified format with system time/date (All other cases)
                availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch));
            }

            // Check searchTerm after getting results to select type of result list
            if (string.IsNullOrEmpty(searchTerm))
            {
                // Generate list with all results
                foreach (var f in availableFormats)
                {
                    results.Add(new Result
                    {
                        Title = f.Value,
                        SubTitle = $"{f.Label} - {Resources.Microsoft_plugin_timedate_SubTitleNote}",
                        ToolTipData = ResultHelper.GetSearchTagToolTip(f, out Visibility v),
                        ToolTipVisibility = v,
                        IcoPath = f.GetIconPath(iconTheme),
                        Action = _ => ResultHelper.CopyToClipBoard(f.Value),
                        ContextData = f,
                    });
                }
            }
            else
            {
                // Generate filtered list of results
                foreach (var f in availableFormats)
                {
                    var resultMatchScore = GetMatchScore(searchTerm, f.Label, f.AlternativeSearchTag, !isKeywordSearch);

                    if (resultMatchScore > 0)
                    {
                        results.Add(new Result
                        {
                            Title = f.Value,
                            SubTitle = $"{f.Label} - {Resources.Microsoft_plugin_timedate_SubTitleNote}",
                            ToolTipData = ResultHelper.GetSearchTagToolTip(f, out Visibility v),
                            ToolTipVisibility = v,
                            IcoPath = f.GetIconPath(iconTheme),
                            Action = _ => ResultHelper.CopyToClipBoard(f.Value),
                            Score = resultMatchScore,
                            ContextData = f,
                        });
                    }
                }
            }

            // If search term is only a number that can't be parsed return an error message
            if (!isEmptySearchInput && results.Count == 0 && Regex.IsMatch(searchTerm, @"\w+[+-]?\d+.*$") && !searchTerm.Any(char.IsWhiteSpace) && (TimeAndDateHelper.IsSpecialInputParsing(searchTerm) || !Regex.IsMatch(searchTerm, @"\d+[\.:/]\d+")))
            {
                string title = !string.IsNullOrEmpty(lastInputParsingErrorReason) ? Resources.Microsoft_plugin_timedate_ErrorResultValue : Resources.Microsoft_plugin_timedate_ErrorResultTitle;
                string message = !string.IsNullOrEmpty(lastInputParsingErrorReason) ? lastInputParsingErrorReason : Resources.Microsoft_plugin_timedate_ErrorResultSubTitle;

                // Without plugin key word show only if not hidden by setting
                if (isKeywordSearch || !TimeDateSettings.Instance.HideNumberMessageOnGlobalQuery)
                {
                    results.Add(ResultHelper.CreateNumberErrorResult(iconTheme, title, message));
                }
            }

            return results;
        }

        /// <summary>
        /// Checks the format for a match with the user query and returns the score.
        /// </summary>
        /// <param name="query">The user query.</param>
        /// <param name="label">The label of the format.</param>
        /// <param name="tags">The search tag list as string.</param>
        /// <param name="isGlobalSearch">Is this a global search?</param>
        /// <returns>The score for the result.</returns>
        private static int GetMatchScore(string query, string label, string tags, bool isGlobalSearch)
        {
            // The query is global and the first word don't match any word in the label or tags => Return score of zero
            if (isGlobalSearch)
            {
                char[] chars = new char[] { ' ', ',', ';', '(', ')' };
                string queryFirstWord = query.Split(chars)[0];
                string[] words = $"{label} {tags}".Split(chars);

                if (!words.Any(x => x.Trim().Equals(queryFirstWord, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return 0;
                }
            }

            // Get match for label (or for tags if label score is <1)
            int score = StringMatcher.FuzzySearch(query, label).Score;
            if (score < 1)
            {
                foreach (string t in tags.Split(";"))
                {
                    var tagScore = StringMatcher.FuzzySearch(query, t.Trim()).Score / 2;
                    if (tagScore > score)
                    {
                        score = tagScore / 2;
                    }
                }
            }

            return score;
        }
    }
}
