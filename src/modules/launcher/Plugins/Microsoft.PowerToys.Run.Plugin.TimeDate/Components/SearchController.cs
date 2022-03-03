// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        /// Searches for results
        /// </summary>
        /// <param name="query">Search query object</param>
        /// <returns>List of Wox <see cref="Result"/>s.</returns>
        internal static List<Result> StartSearch(Query query, string iconTheme)
        {
            List<AvailableResult> availableFormats = new List<AvailableResult>();
            List<Result> results = new List<Result>();
            bool isKeywordSearch = !string.IsNullOrEmpty(query.ActionKeyword);
            bool isEmptySearchInput = string.IsNullOrEmpty(query.Search);
            string searchTerm = query.Search;

            // empty search without keyword => return no results
            if (!isKeywordSearch && isEmptySearchInput)
            {
                return results;
            }

            // Switch search type
            if (isEmptySearchInput)
            {
                // Return all results for system time/date on empty keyword search
                availableFormats.AddRange(ResultHelper.GetAvailableResults(isKeywordSearch));
            }
            else if (searchTerm.All(char.IsLetter))
            {
                // Search for specified format with system time/date
                availableFormats.AddRange(ResultHelper.GetAvailableResults(isKeywordSearch));
            }
            else if (Regex.IsMatch(searchTerm, @".+" + Regex.Escape(InputDelimiter) + @".+"))
            {
                // Search for specified format with specified time/date value
                var userInput = searchTerm.Split(InputDelimiter);
                if (TimeAndDateHelper.ParseStringAsDateTime(userInput[1], out DateTime timestamp))
                {
                    availableFormats.AddRange(ResultHelper.GetAvailableResults(isKeywordSearch, null, null, timestamp));
                    searchTerm = userInput[0];
                }
            }
            else if (TimeAndDateHelper.ParseStringAsDateTime(searchTerm, out DateTime timestamp))
            {
                // Return all formats for specified time/date value
                availableFormats.AddRange(ResultHelper.GetAvailableResults(isKeywordSearch, null, null, timestamp));
                searchTerm = string.Empty;
            }
            else if (searchTerm.Any(char.IsNumber) && !searchTerm.Any(char.IsSymbol))
            {
                // If search term contains a number that can't be parsed return an error message
                results.Add(GetNumberErrorResult(iconTheme));
                return results;
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
                        SubTitle = $"{f.Label} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = f.GetIconPath(iconTheme),
                        Action = _ => ResultHelper.CopyToClipBoard(f.Value),
                    });
                }
            }
            else
            {
                // Generate filtered list of results
                foreach (var f in availableFormats)
                {
                    var resultMatch = StringMatcher.FuzzySearch(searchTerm, f.Label);
                    if (resultMatch.Score > 0)
                    {
                        results.Add(new Result
                        {
                            Title = f.Value,
                            SubTitle = $"{f.Label} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                            IcoPath = f.GetIconPath(iconTheme),
                            Action = _ => ResultHelper.CopyToClipBoard(f.Value),
                            Score = resultMatch.Score,
                            SubTitleHighlightData = resultMatch.MatchData,
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets a result with an error message that only numbers can't be parsed
        /// </summary>
        /// <returns>Element of type <see cref="Result"/>.</returns>
        private static Result GetNumberErrorResult(string theme) => new Result()
        {
            Title = Resources.Microsoft_plugin_timedate_ErrorResultTitle,
            SubTitle = Resources.Microsoft_plugin_timedate_ErrorResultSubTitle,
            IcoPath = $"Images\\Warning.{theme}.png",
        };
    }
}
