// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal static class CustomClockDisplay
{
    private const string RelativeDayToken = "REL";

    internal static DateTimeOffset GetCurrentTime(CustomClock clock, DateTimeOffset? utcNow = null) => TimeZoneInfo.ConvertTime(
        utcNow ?? DateTimeOffset.UtcNow,
        clock.TimeZoneId == CustomClock.CurrentTimeZoneId ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId));

    internal static string GetName(CustomClock clock, DateTimeOffset? utcNow = null)
    {
        if (!string.IsNullOrWhiteSpace(clock.Title))
        {
            return clock.Title;
        }

        var timeZone = clock.TimeZoneId == CustomClock.CurrentTimeZoneId ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        var currentTime = GetCurrentTime(clock, utcNow);
        return timeZone.IsDaylightSavingTime(currentTime) && !string.IsNullOrWhiteSpace(timeZone.DaylightName)
            ? timeZone.DaylightName
            : timeZone.StandardName;
    }

    internal static string GetLocalOffsetDifference(DateTimeOffset clockTime)
    {
        var localTime = TimeZoneInfo.ConvertTime(clockTime, TimeZoneInfo.Local);
        var difference = clockTime.Offset - localTime.Offset;
        if (difference == TimeSpan.Zero)
        {
            return string.Empty;
        }

        var absoluteDifference = difference.Duration();
        var minutes = absoluteDifference.Minutes == 0 ? string.Empty : $" {absoluteDifference.Minutes}m";
        return $"{(difference < TimeSpan.Zero ? "−" : "+")}{absoluteDifference.Hours}h{minutes}";
    }

    internal static string Format(DateTimeOffset time, string format, ISettingsInterface settings)
    {
        if (string.IsNullOrEmpty(format))
        {
            return string.Empty;
        }

        var relativeText = (time.Date - DateTime.Now.Date).Days switch
        {
            -1 => Resources.timedate_relative_yesterday,
            0 => Resources.timedate_relative_today,
            1 => Resources.timedate_relative_tomorrow,
            _ => string.Empty,
        };
        var relativeLiteral = $"'{relativeText.Replace("'", "''", StringComparison.Ordinal)}'";
        var date = time.DateTime;
        var utc = time.UtcDateTime;
        var formatWithoutUtcPrefix = format.StartsWith("UTC:", StringComparison.Ordinal) ? format[4..] : format;
        if (format.StartsWith("UTC:", StringComparison.Ordinal))
        {
            date = utc;
        }

        var rule = TimeAndDateHelper.GetCalendarWeekRule(settings.FirstWeekOfYear);
        var firstDay = TimeAndDateHelper.GetFirstDayOfWeek(settings.FirstDayOfWeek);
        var calendar = CultureInfo.CurrentCulture.Calendar;
        var converted = TimeAndDateHelper.ConvertToCustomFormat(
            date,
            new DateTimeOffset(utc).ToUnixTimeSeconds(),
            new DateTimeOffset(utc).ToUnixTimeMilliseconds(),
            calendar.GetWeekOfYear(date, rule, firstDay),
            DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(calendar.GetEra(date)),
            formatWithoutUtcPrefix.Replace("{relative}", RelativeDayToken, StringComparison.Ordinal).Replace(RelativeDayToken, relativeLiteral, StringComparison.Ordinal),
            rule,
            firstDay);
        return date.ToString(converted, CultureInfo.CurrentCulture);
    }

    internal static bool RequiresSecondUpdates(CustomClock clock) => RequiresSecondUpdates(clock.TitleFormat) || RequiresSecondUpdates(clock.SubtitleFormat);

    internal static bool RequiresSecondUpdates(string titleFormat, string subtitleFormat) => RequiresSecondUpdates(titleFormat) || RequiresSecondUpdates(subtitleFormat);

    internal static string NormalizeRelativeDayToken(string format) => format.Replace("{relative}", RelativeDayToken, StringComparison.Ordinal);

    private static bool RequiresSecondUpdates(string format)
    {
        var formatWithoutUtcPrefix = format.StartsWith("UTC:", StringComparison.Ordinal) ? format[4..] : format;
        if (formatWithoutUtcPrefix is "UXT" or "UMS" or "WFT")
        {
            return true;
        }

        if (formatWithoutUtcPrefix is "t" or "f" or "g")
        {
            return ContainsSecondToken(DateTimeFormatInfo.CurrentInfo.ShortTimePattern);
        }

        if (formatWithoutUtcPrefix is "T" or "F" or "G")
        {
            return ContainsSecondToken(DateTimeFormatInfo.CurrentInfo.LongTimePattern);
        }

        if (formatWithoutUtcPrefix.Length == 1)
        {
            return formatWithoutUtcPrefix[0] is 'O' or 'o' or 'R' or 's' or 'U' or 'u';
        }

        return ContainsSecondToken(formatWithoutUtcPrefix);
    }

    private static bool ContainsSecondToken(string format)
    {
        var quoted = false;
        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '\\')
            {
                index++;
            }
            else if (character == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && character is 's' or 'f' or 'F')
            {
                return true;
            }
        }

        return false;
    }
}
