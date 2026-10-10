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
    /// <returns>List of list items for the query.</returns>
    public static List<ListItem> ExecuteSearch(ISettingsInterface settings, string query, DateTimeOffset? currentTime = null)
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
            availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings, currentTime: currentTime));
        }
        else if (Regex.IsMatch(query, @".+" + Regex.Escape(InputDelimiter) + @".+"))
        {
            // Search for specified format with specified time/date value
            var userInput = query.Split(InputDelimiter);
            if (TryGetResultsForInput(userInput[1], isKeywordSearch, settings, currentTime, availableFormats, out lastInputParsingErrorMsg))
            {
                query = userInput[0];
            }
        }
        else if (TryGetResultsForInput(query, isKeywordSearch, settings, currentTime, availableFormats, out lastInputParsingErrorMsg))
        {
            // Return all formats for specified time/date value or calculation
            query = string.Empty;
        }
        else
        {
            // Search for specified format with system time/date (All other cases)
            availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings, currentTime: currentTime));
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

    /// <summary>
    /// Adds the results for a date/time value, or for a date calculation like "today + 3d" or "2025-12-25 - today".
    /// </summary>
    /// <returns>True if the input could be parsed; otherwise, false.</returns>
    private static bool TryGetResultsForInput(string input, bool isKeywordSearch, ISettingsInterface settings, DateTimeOffset? currentTime, List<AvailableResult> availableFormats, out string inputParsingErrorMsg)
    {
        // Plain date/time values go first, so input that already parses keeps its meaning.
        if (DateTimeInputParser.ParseStringAsDateTime(input, out DateTime timestamp, out inputParsingErrorMsg))
        {
            availableFormats.AddRange(AvailableResultsList.GetList(isKeywordSearch, settings, null, null, timestamp));
            return true;
        }

        var now = currentTime?.DateTime ?? DateTime.Now;
        if (DateCalculationParser.TryEvaluate(input, now, out var calculation, out var calculationErrorMsg))
        {
            availableFormats.AddRange(calculation!.IsDifference
                ? AvailableResultsList.GetDifferenceList(calculation.From!.Value, calculation.To!.Value)
                : AvailableResultsList.GetList(isKeywordSearch, settings, null, null, calculation.Timestamp));
            return true;
        }

        if (!string.IsNullOrEmpty(calculationErrorMsg))
        {
            inputParsingErrorMsg = calculationErrorMsg;
        }

        return false;
    }
}
