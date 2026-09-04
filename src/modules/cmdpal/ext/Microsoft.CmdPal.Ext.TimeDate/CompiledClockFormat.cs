// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate;

/// <summary>
/// Holds the format information that is invariant between clock updates.
/// </summary>
internal sealed class CompiledClockFormat
{
    private const string RelativeDayToken = "REL";

    private readonly string _format;
    private readonly FormatSegment[] _segments;
    private readonly CustomFormatToken _tokens;

    internal CompiledClockFormat(string format)
    {
        IsEmpty = string.IsNullOrEmpty(format);
        if (IsEmpty)
        {
            _format = string.Empty;
            _segments = [];
            return;
        }

        IsUtc = format.StartsWith("UTC:", StringComparison.Ordinal);
        _format = IsUtc ? format[4..] : format;
        (_segments, _tokens) = ParseSegments(_format);
        _ = DateTime.UtcNow.ToString(GetStandardFormat(_format, _segments, _tokens), CultureInfo.CurrentCulture);
        RequiresSecondUpdates = DetermineRequiresSecondUpdates(_format, _segments, _tokens);
    }

    internal bool IsEmpty { get; }

    internal bool IsUtc { get; }

    internal bool RequiresSecondUpdates { get; }

    internal string Format(DateTimeOffset time, ISettingsInterface settings)
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var date = IsUtc ? time.UtcDateTime : time.DateTime;
        var displayTime = IsUtc ? time.ToUniversalTime() : time;
        var culture = CultureInfo.CurrentCulture;
        if (_tokens == CustomFormatToken.None)
        {
            return FormatDateTime(displayTime, _format, culture);
        }

        var firstDay = (_tokens & (CustomFormatToken.DayOfWeek | CustomFormatToken.WeekOfMonth | CustomFormatToken.WeekOfYear)) != 0
            ? TimeAndDateHelper.GetFirstDayOfWeek(settings.FirstDayOfWeek)
            : default;
        var calendar = (_tokens & CustomFormatToken.EraAbbreviation) != 0
            ? culture.Calendar
            : null;

        // Values substituted for tokens are spliced back into the format string, so a token
        // whose value is text must be escaped with ToDateTimeFormatLiteral or it gets re-parsed
        // as format specifiers. Numeric tokens need no escaping: digits are not specifiers.
        string? dayOfWeek = null;
        string? daysInMonth = null;
        string? weekOfMonth = null;
        string? weekOfYear = null;
        string? isoWeekOfYear = null;
        string? isoWeekYear = null;
        string? isoWeekYearShort = null;
        string? isoDayOfWeek = null;
        string? eraAbbreviation = null;
        string? windowsFileTime = null;
        string? unixTime = null;
        string? unixTimeMilliseconds = null;
        string? oleAutomationDate = null;
        string? excel1900Date = null;
        string? excel1904Date = null;
        string? relativeDay = null;

        if ((_tokens & CustomFormatToken.DayOfWeek) != 0)
        {
            dayOfWeek = TimeAndDateHelper.GetNumberOfDayInWeek(date, firstDay).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.DaysInMonth) != 0)
        {
            daysInMonth = DateTime.DaysInMonth(date.Year, date.Month).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.WeekOfMonth) != 0)
        {
            weekOfMonth = TimeAndDateHelper.GetWeekOfMonth(date, firstDay).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.WeekOfYear) != 0)
        {
            var rule = TimeAndDateHelper.GetCalendarWeekRule(settings.FirstWeekOfYear);
            weekOfYear = TimeAndDateHelper.GetWeekOfYear(date, rule, firstDay).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.IsoWeekOfYear) != 0)
        {
            isoWeekOfYear = ISOWeek.GetWeekOfYear(date).ToString("D2", culture);
        }

        if ((_tokens & CustomFormatToken.IsoWeekYear) != 0)
        {
            isoWeekYear = ISOWeek.GetYear(date).ToString("D4", culture);
        }

        if ((_tokens & CustomFormatToken.IsoWeekYearShort) != 0)
        {
            isoWeekYearShort = (ISOWeek.GetYear(date) % 100).ToString("D2", culture);
        }

        if ((_tokens & CustomFormatToken.IsoDayOfWeek) != 0)
        {
            isoDayOfWeek = (date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.EraAbbreviation) != 0)
        {
            eraAbbreviation = ToDateTimeFormatLiteral(culture.DateTimeFormat.GetAbbreviatedEraName(calendar!.GetEra(date)));
        }

        if ((_tokens & CustomFormatToken.WindowsFileTime) != 0)
        {
            windowsFileTime = time.ToFileTime().ToString(culture);
        }

        if ((_tokens & (CustomFormatToken.UnixTime | CustomFormatToken.UnixTimeMilliseconds)) != 0)
        {
            var utc = new DateTimeOffset(time.UtcDateTime);
            if ((_tokens & CustomFormatToken.UnixTime) != 0)
            {
                unixTime = utc.ToUnixTimeSeconds().ToString(culture);
            }

            if ((_tokens & CustomFormatToken.UnixTimeMilliseconds) != 0)
            {
                unixTimeMilliseconds = utc.ToUnixTimeMilliseconds().ToString(culture);
            }
        }

        if ((_tokens & CustomFormatToken.OleAutomationDate) != 0)
        {
            oleAutomationDate = TimeAndDateHelper.ConvertToOleAutomationFormat(date, OADateFormats.OLEAutomation).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.Excel1900Date) != 0)
        {
            excel1900Date = TimeAndDateHelper.ConvertToOleAutomationFormat(date, OADateFormats.Excel1900).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.Excel1904Date) != 0)
        {
            excel1904Date = TimeAndDateHelper.ConvertToOleAutomationFormat(date, OADateFormats.Excel1904).ToString(culture);
        }

        if ((_tokens & CustomFormatToken.RelativeDay) != 0)
        {
            var relativeText = (time.Date - DateTime.Now.Date).Days switch
            {
                -1 => Resources.timedate_relative_yesterday,
                0 => Resources.timedate_relative_today,
                1 => Resources.timedate_relative_tomorrow,
                _ => string.Empty,
            };
            relativeDay = ToDateTimeFormatLiteral(relativeText);
        }

        var converted = new StringBuilder(_format.Length + 32);
        foreach (var segment in _segments)
        {
            if (segment.Token == CustomFormatToken.None)
            {
                converted.Append(segment.Literal);
                continue;
            }

            converted.Append(segment.Token switch
            {
                CustomFormatToken.DayOfWeek => dayOfWeek,
                CustomFormatToken.DaysInMonth => daysInMonth,
                CustomFormatToken.WeekOfMonth => weekOfMonth,
                CustomFormatToken.WeekOfYear => weekOfYear,
                CustomFormatToken.IsoWeekOfYear => isoWeekOfYear,
                CustomFormatToken.IsoWeekYear => isoWeekYear,
                CustomFormatToken.IsoWeekYearShort => isoWeekYearShort,
                CustomFormatToken.IsoDayOfWeek => isoDayOfWeek,
                CustomFormatToken.EraAbbreviation => eraAbbreviation,
                CustomFormatToken.WindowsFileTime => windowsFileTime,
                CustomFormatToken.UnixTime => unixTime,
                CustomFormatToken.UnixTimeMilliseconds => unixTimeMilliseconds,
                CustomFormatToken.OleAutomationDate => oleAutomationDate,
                CustomFormatToken.Excel1900Date => excel1900Date,
                CustomFormatToken.Excel1904Date => excel1904Date,
                CustomFormatToken.RelativeDay => relativeDay,
                _ => string.Empty,
            });
        }

        var convertedFormat = converted.ToString();
        return convertedFormat.Length == 1 ? convertedFormat : FormatDateTime(displayTime, convertedFormat, culture);
    }

    private static string FormatDateTime(DateTimeOffset time, string format, CultureInfo culture) =>
        format == "U" ? time.UtcDateTime.ToString("F", culture) : time.ToString(format, culture);

    internal static string ToDateTimeFormatLiteral(string text)
    {
        var escapedText = text
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("'", @"\'", StringComparison.Ordinal);
        return $"'{escapedText}'";
    }

    private static (FormatSegment[] Segments, CustomFormatToken Tokens) ParseSegments(string format)
    {
        var segments = new List<FormatSegment>();
        var tokens = CustomFormatToken.None;
        var literalStart = 0;

        for (var index = 0; index <= format.Length - 3; index++)
        {
            var tokenLength = 3;
            if (index <= format.Length - 4 && TryGetToken(format.AsSpan(index, 4), out var token))
            {
                tokenLength = 4;
            }
            else if (!TryGetToken(format.AsSpan(index, 3), out token))
            {
                continue;
            }

            if (index > 0 && format[index - 1] == '\\')
            {
                index += tokenLength - 1;
                continue;
            }

            if (index > literalStart)
            {
                segments.Add(new(format[literalStart..index], CustomFormatToken.None));
            }

            segments.Add(new(null, token));
            tokens |= token;
            index += tokenLength - 1;
            literalStart = index + 1;
        }

        if (literalStart < format.Length)
        {
            segments.Add(new(format[literalStart..], CustomFormatToken.None));
        }

        return tokens == CustomFormatToken.None ? ([], tokens) : ([.. segments], tokens);
    }

    private static bool TryGetToken(ReadOnlySpan<char> value, out CustomFormatToken token)
    {
        token = value switch
        {
            "DOW" => CustomFormatToken.DayOfWeek,
            "DIM" => CustomFormatToken.DaysInMonth,
            "WOM" => CustomFormatToken.WeekOfMonth,
            "WOY" => CustomFormatToken.WeekOfYear,
            "IWOY" => CustomFormatToken.IsoWeekOfYear,
            "IWYR" => CustomFormatToken.IsoWeekYear,
            "IWYY" => CustomFormatToken.IsoWeekYearShort,
            "IDOW" => CustomFormatToken.IsoDayOfWeek,
            "EAB" => CustomFormatToken.EraAbbreviation,
            "WFT" => CustomFormatToken.WindowsFileTime,
            "UXT" => CustomFormatToken.UnixTime,
            "UMS" => CustomFormatToken.UnixTimeMilliseconds,
            "OAD" => CustomFormatToken.OleAutomationDate,
            "EXC" => CustomFormatToken.Excel1900Date,
            "EXF" => CustomFormatToken.Excel1904Date,
            RelativeDayToken => CustomFormatToken.RelativeDay,
            _ => CustomFormatToken.None,
        };
        return token != CustomFormatToken.None;
    }

    private static bool DetermineRequiresSecondUpdates(string format, FormatSegment[] segments, CustomFormatToken tokens)
    {
        if ((tokens & (CustomFormatToken.WindowsFileTime |
                       CustomFormatToken.UnixTime |
                       CustomFormatToken.UnixTimeMilliseconds |
                       CustomFormatToken.OleAutomationDate |
                       CustomFormatToken.Excel1900Date |
                       CustomFormatToken.Excel1904Date)) != 0)
        {
            return true;
        }

        if (tokens == CustomFormatToken.None)
        {
            if (format is "t" or "f" or "g")
            {
                return ContainsSecondToken(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern);
            }

            if (format is "T" or "F" or "G")
            {
                return ContainsSecondToken(CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern);
            }

            if (format.Length == 1)
            {
                return format[0] is 'O' or 'o' or 'R' or 'r' or 's' or 'U' or 'u';
            }

            return ContainsSecondToken(format);
        }

        return ContainsSecondToken(GetStandardFormat(format, segments, tokens));
    }

    private static string GetStandardFormat(string format, FormatSegment[] segments, CustomFormatToken tokens)
    {
        if (tokens == CustomFormatToken.None)
        {
            return format;
        }

        var standardFormat = new StringBuilder(format.Length);
        foreach (var segment in segments)
        {
            standardFormat.Append(segment.Token == CustomFormatToken.None ? segment.Literal : "000");
        }

        return standardFormat.ToString();
    }

    private static bool ContainsSecondToken(string format)
    {
        char quote = default;
        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '\\')
            {
                index++;
            }
            else if (character is '\'' or '"')
            {
                if (quote == default)
                {
                    quote = character;
                }
                else if (quote == character)
                {
                    quote = default;
                }
            }
            else if (quote == default && character is 's' or 'f' or 'F')
            {
                return true;
            }
        }

        return false;
    }

    [Flags]
    private enum CustomFormatToken
    {
        None = 0,
        DayOfWeek = 1 << 0,
        DaysInMonth = 1 << 1,
        WeekOfMonth = 1 << 2,
        WeekOfYear = 1 << 3,
        EraAbbreviation = 1 << 4,
        WindowsFileTime = 1 << 5,
        UnixTime = 1 << 6,
        UnixTimeMilliseconds = 1 << 7,
        OleAutomationDate = 1 << 8,
        Excel1900Date = 1 << 9,
        Excel1904Date = 1 << 10,
        RelativeDay = 1 << 11,
        IsoWeekOfYear = 1 << 12,
        IsoWeekYear = 1 << 13,
        IsoWeekYearShort = 1 << 14,
        IsoDayOfWeek = 1 << 15,
    }

    private readonly record struct FormatSegment(string? Literal, CustomFormatToken Token);
}
