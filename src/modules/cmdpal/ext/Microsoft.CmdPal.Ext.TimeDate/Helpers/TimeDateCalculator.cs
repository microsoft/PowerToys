// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

public sealed partial class TimeDateCalculator
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
    public static List<ListItem> ExecuteSearch(ISettingsInterface settings, string query)
    {
        var isEmptySearchInput = string.IsNullOrWhiteSpace(query);
        List<AvailableResult> availableFormats = new List<AvailableResult>();
        List<ListItem> results = new List<ListItem>();

        // currently, all of the search in V2 is keyword search.
        var isKeywordSearch = true;

        // Last input parsing error
        var lastInputParsingErrorMsg = string.Empty;

        // Switch search type
        if (isEmptySearchInput || (!isKeywordSearch))
        {
            // Return all results for system time/date on empty keyword search
            // or only time, date and now results for system time on global queries if the corresponding setting is enabled
            availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings));
        }
        else if (Regex.IsMatch(query, @".+" + Regex.Escape(InputDelimiter) + @".+"))
        {
            // Search for specified format with specified time/date value
            var userInput = query.Split(InputDelimiter);
            if (TimeAndDateHelper.ParseStringAsDateTime(userInput[1], out DateTime timestamp, out lastInputParsingErrorMsg))
            {
                availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings, null, null, timestamp));
                query = userInput[0];
            }
        }
        else if (TimeAndDateHelper.ParseStringAsDateTime(query, out DateTime timestamp, out lastInputParsingErrorMsg))
        {
            // Return all formats for specified time/date value
            availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings, null, null, timestamp));
            query = string.Empty;
        }
        else
        {
            // Search for specified format with system time/date (All other cases)
            availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings));
        }

        // Check searchTerm after getting results to select type of result list
        if (string.IsNullOrEmpty(query))
        {
            // Generate list with all results
            foreach (var f in availableFormats)
            {
                results.Add(f.ToListItem());
            }
        }
        else
        {
            List<(int Score, AvailableResult Item)> itemScores = [];

            // Generate filtered list of results
            foreach (var f in availableFormats)
            {
                var score = f.Score(query, f.Label, f.AlternativeSearchTag);
                if (score > 0)
                {
                    itemScores.Add((score, f));
                }
            }

            results = itemScores
                        .OrderByDescending(s => s.Score)
                        .Select(s => s.Item.ToListItem())
                        .ToList();
        }

        if (results.Count == 0)
        {
            var er = ResultHelper.CreateInvalidInputErrorResult();
            if (!string.IsNullOrEmpty(lastInputParsingErrorMsg))
            {
                er.Details = new Details() { Body = lastInputParsingErrorMsg };
            }

            results.Add(er);
        }

        return results;
    }
}
