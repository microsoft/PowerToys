// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

/// <summary>
/// Evaluates simple date calculations:
/// <list type="bullet">
/// <item>a date plus or minus one or more durations, e.g. <c>today + 30 days</c> or <c>2025-06-27 - 2w + 3d</c></item>
/// <item>the difference between two dates, e.g. <c>2025.06.30 - 2025.06.27</c> or <c>2025-12-25 - today</c></item>
/// </list>
/// A date is anything <see cref="DateTimeInputParser"/> understands, or one of the keywords
/// now, today, tomorrow and yesterday. Durations always need a unit, so input like
/// <c>2025-06-27+1</c> keeps its existing meaning (a UTC offset).
/// </summary>
internal static partial class DateCalculationParser
{
    // A trailing "+ 3 days" / "-2w" term. The greedy prefix makes this match the last operator.
    [GeneratedRegex(@"^(?<rest>.*\S)\s*(?<op>[+-])\s*(?<number>\d+)\s*(?<unit>\p{L}+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDurationRegex();

    // Inputs that DateTimeInputParser handles through its prefixed number formats.
    [GeneratedRegex(@"^(u|ums|ft|oa|exc|exf)[+-]?\d", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixedNumberRegex();

    private static readonly Dictionary<string, DurationUnit> InvariantUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["y"] = DurationUnit.Years,
        ["yr"] = DurationUnit.Years,
        ["yrs"] = DurationUnit.Years,
        ["year"] = DurationUnit.Years,
        ["years"] = DurationUnit.Years,
        ["mo"] = DurationUnit.Months,
        ["mon"] = DurationUnit.Months,
        ["month"] = DurationUnit.Months,
        ["months"] = DurationUnit.Months,
        ["w"] = DurationUnit.Weeks,
        ["wk"] = DurationUnit.Weeks,
        ["wks"] = DurationUnit.Weeks,
        ["week"] = DurationUnit.Weeks,
        ["weeks"] = DurationUnit.Weeks,
        ["d"] = DurationUnit.Days,
        ["day"] = DurationUnit.Days,
        ["days"] = DurationUnit.Days,
        ["h"] = DurationUnit.Hours,
        ["hr"] = DurationUnit.Hours,
        ["hrs"] = DurationUnit.Hours,
        ["hour"] = DurationUnit.Hours,
        ["hours"] = DurationUnit.Hours,
        ["m"] = DurationUnit.Minutes,
        ["min"] = DurationUnit.Minutes,
        ["mins"] = DurationUnit.Minutes,
        ["minute"] = DurationUnit.Minutes,
        ["minutes"] = DurationUnit.Minutes,
        ["s"] = DurationUnit.Seconds,
        ["sec"] = DurationUnit.Seconds,
        ["secs"] = DurationUnit.Seconds,
        ["second"] = DurationUnit.Seconds,
        ["seconds"] = DurationUnit.Seconds,
    };

    private static readonly Dictionary<string, DateKeyword> InvariantKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["now"] = DateKeyword.Now,
        ["today"] = DateKeyword.Today,
        ["tomorrow"] = DateKeyword.Tomorrow,
        ["yesterday"] = DateKeyword.Yesterday,
    };

    /// <summary>
    /// Tries to evaluate <paramref name="input"/> as a date calculation.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="now">The current local time, used for the date keywords.</param>
    /// <param name="result">The evaluated calculation.</param>
    /// <param name="errorMessage">Error shown to the user when the input is a calculation that can't be evaluated; otherwise empty.</param>
    /// <returns>True if the input is a calculation that could be evaluated; otherwise, false.</returns>
    internal static bool TryEvaluate(string? input, DateTime now, out DateCalculationResult? result, out string errorMessage)
    {
        result = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var expression = input.Trim();
        var durations = new List<(int Sign, int Amount, DurationUnit Unit)>();

        // Peel duration terms off the end until the remainder is the base date.
        Match match;
        while ((match = TrailingDurationRegex().Match(expression)).Success
            && TryGetUnit(match.Groups["unit"].Value, out var unit)
            && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            durations.Insert(0, (match.Groups["op"].Value == "-" ? -1 : 1, amount, unit));
            expression = match.Groups["rest"].Value;
        }

        try
        {
            if (durations.Count > 0)
            {
                if (!TryParseDate(expression, now, out var timestamp, out _))
                {
                    return false;
                }

                foreach (var (sign, amount, unit) in durations)
                {
                    timestamp = Add(timestamp, unit, sign * amount);
                }

                result = new DateCalculationResult(timestamp);
                return true;
            }

            if (TryParseDifference(expression, now, out var from, out var to))
            {
                result = new DateCalculationResult(from, to);
                return true;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            errorMessage = Resources.Microsoft_plugin_timedate_InvalidInput_CalculationOutOfRange;
        }

        return false;
    }

    /// <summary>
    /// Splits "a - b" into two dates. Dates can contain '-' themselves (2024-01-01), so every
    /// '-' is tried. Splits where a side only parses by reading a trailing "-01" as a UTC offset
    /// are dropped, and a '-' surrounded by spaces wins over one that isn't.
    /// </summary>
    private static bool TryParseDifference(string expression, DateTime now, out DateTime from, out DateTime to)
    {
        from = default;
        to = default;
        var bestRank = int.MaxValue;
        var bestCount = 0;

        for (var i = expression.IndexOf('-', 1); i > 0 && i < expression.Length - 1; i = expression.IndexOf('-', i + 1))
        {
            var left = expression[..i];
            var right = expression[(i + 1)..];
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)
                || !TryParseDate(left.Trim(), now, out var leftDate, out var leftHasOffset)
                || !TryParseDate(right.Trim(), now, out var rightDate, out var rightHasOffset))
            {
                continue;
            }

            var spaced = char.IsWhiteSpace(expression[i - 1]) && char.IsWhiteSpace(expression[i + 1]);
            var rank = (leftHasOffset || rightHasOffset ? 2 : 0) + (spaced ? 0 : 1);

            if (rank < bestRank)
            {
                bestRank = rank;
                bestCount = 1;
                to = leftDate;
                from = rightDate;
            }
            else if (rank == bestRank)
            {
                bestCount++;
            }
        }

        // More than one equally good split means the input is ambiguous.
        return bestCount == 1;
    }

    private static bool TryParseDate(string input, DateTime now, out DateTime timestamp, out bool hasOffset)
    {
        hasOffset = false;

        if (TryGetKeyword(input, out var keyword))
        {
            timestamp = keyword switch
            {
                DateKeyword.Today => now.Date,
                DateKeyword.Tomorrow => now.Date.AddDays(1),
                DateKeyword.Yesterday => now.Date.AddDays(-1),
                _ => now,
            };
            return true;
        }

        if (DateTime.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.None, out timestamp))
        {
            // DateTime.TryParse only returns a local time when the input carried a UTC offset.
            hasOffset = timestamp.Kind == DateTimeKind.Local;
            return true;
        }

        // Only hand prefixed numbers to DateTimeInputParser, so that probing every possible
        // split doesn't log a failure for each one.
        return PrefixedNumberRegex().IsMatch(input)
            && DateTimeInputParser.ParseStringAsDateTime(input, out timestamp, out _);
    }

    private static DateTime Add(DateTime timestamp, DurationUnit unit, int amount) => unit switch
    {
        DurationUnit.Years => timestamp.AddYears(amount),
        DurationUnit.Months => timestamp.AddMonths(amount),
        DurationUnit.Weeks => timestamp.AddDays(amount * 7.0),
        DurationUnit.Days => timestamp.AddDays(amount),
        DurationUnit.Hours => timestamp.AddHours(amount),
        DurationUnit.Minutes => timestamp.AddMinutes(amount),
        _ => timestamp.AddSeconds(amount),
    };

    private static bool TryGetUnit(string text, out DurationUnit unit)
    {
        if (InvariantUnits.TryGetValue(text, out unit))
        {
            return true;
        }

        (string Names, DurationUnit Unit)[] localized =
        [
            (Resources.Microsoft_plugin_timedate_Calculation_UnitYears, DurationUnit.Years),
            (Resources.Microsoft_plugin_timedate_Calculation_UnitMonths, DurationUnit.Months),
            (Resources.Microsoft_plugin_timedate_Calculation_UnitWeeks, DurationUnit.Weeks),
            (Resources.Microsoft_plugin_timedate_Calculation_UnitDays, DurationUnit.Days),
            (Resources.Microsoft_plugin_timedate_Calculation_UnitHours, DurationUnit.Hours),
            (Resources.Microsoft_plugin_timedate_Calculation_UnitMinutes, DurationUnit.Minutes),
            (Resources.Microsoft_plugin_timedate_Calculation_UnitSeconds, DurationUnit.Seconds),
        ];

        foreach (var (names, localizedUnit) in localized)
        {
            if (ContainsName(names, text))
            {
                unit = localizedUnit;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetKeyword(string text, out DateKeyword keyword)
    {
        if (InvariantKeywords.TryGetValue(text, out keyword))
        {
            return true;
        }

        (string Names, DateKeyword Keyword)[] localized =
        [
            (Resources.Microsoft_plugin_timedate_Calculation_KeywordNow, DateKeyword.Now),
            (Resources.Microsoft_plugin_timedate_Calculation_KeywordToday, DateKeyword.Today),
            (Resources.Microsoft_plugin_timedate_Calculation_KeywordTomorrow, DateKeyword.Tomorrow),
            (Resources.Microsoft_plugin_timedate_Calculation_KeywordYesterday, DateKeyword.Yesterday),
        ];

        foreach (var (names, localizedKeyword) in localized)
        {
            if (ContainsName(names, text))
            {
                keyword = localizedKeyword;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsName(string names, string text)
    {
        foreach (var name in names.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private enum DurationUnit
    {
        Years,
        Months,
        Weeks,
        Days,
        Hours,
        Minutes,
        Seconds,
    }

    private enum DateKeyword
    {
        Now,
        Today,
        Tomorrow,
        Yesterday,
    }
}
