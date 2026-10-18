// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

internal static class TimeAndDateHelper
{
    /// <summary>
    /// Get the format for the time string
    /// </summary>
    /// <param name="targetFormat">Type of format</param>
    /// <param name="timeLong">Show date with weekday and name of month (long format)</param>
    /// <param name="dateLong">Show time with seconds (long format)</param>
    /// <returns>String that identifies the time/date format (<see href="https://learn.microsoft.com/dotnet/api/system.datetime.tostring"/>)</returns>
    internal static string GetStringFormat(FormatStringType targetFormat, bool timeLong, bool dateLong)
    {
        switch (targetFormat)
        {
            case FormatStringType.Time:
                return timeLong ? "T" : "t";
            case FormatStringType.Date:
                return dateLong ? "D" : "d";
            case FormatStringType.DateTime:
                if (timeLong & dateLong)
                {
                    return "F"; // Friday, October 31, 2008 5:04:32 PM
                }
                else if (timeLong & !dateLong)
                {
                    return "G"; // 10/31/2008 5:04:32 PM
                }
                else if (!timeLong & dateLong)
                {
                    return "f"; // Friday, October 31, 2008 5:04 PM
                }
                else
                {
                    // (!timeLong & !dateLong)
                    return "g"; // 10/31/2008 5:04 PM
                }

            default:
                return string.Empty; // Windows default based on current culture settings
        }
    }

    /// <summary>
    /// Returns the number week in the month (Used code from 'David Morton' from <see href="https://social.msdn.microsoft.com/Forums/vstudio/bf504bba-85cb-492d-a8f7-4ccabdf882cb/get-week-number-for-month"/>)
    /// </summary>
    /// <param name="date">date</param>
    /// <param name="formatSettingFirstDayOfWeek">Setting for the first day in the week.</param>
    /// <returns>Number of week in the month</returns>
    internal static int GetWeekOfMonth(DateTime date, DayOfWeek formatSettingFirstDayOfWeek)
    {
        var weekCount = 1;

        for (var i = 1; i <= date.Day; i++)
        {
            DateTime d = new(date.Year, date.Month, i);

            // Count week number +1 if day is the first day of a week and not day 1 of the month.
            // (If we count on day one of a month we would start the month with week number 2.)
            if (i > 1 && d.DayOfWeek == formatSettingFirstDayOfWeek)
            {
                weekCount += 1;
            }
        }

        return weekCount;
    }

    /// <summary>
    /// Returns the number of the day in the week
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Number of the day in the week</returns>
    internal static int GetNumberOfDayInWeek(DateTime date, DayOfWeek formatSettingFirstDayOfWeek)
    {
        const int daysInWeek = 7;
        const int adjustment = 1; // We count from 1 to 7 and not from 0 to 6

        return ((date.DayOfWeek + daysInWeek - formatSettingFirstDayOfWeek) % daysInWeek) + adjustment;
    }

    internal static double ConvertToOleAutomationFormat(DateTime date, OADateFormats type)
    {
        var v = date.ToOADate();

        switch (type)
        {
            case OADateFormats.Excel1904:
                // Excel with base 1904: Adjust by -1462
                v -= 1462;

                // Date starts at 1/1/1904 = 0
                if (Math.Truncate(v) < 0)
                {
                    throw new ArgumentOutOfRangeException("Not a valid Excel date.", innerException: null);
                }

                return v;
            case OADateFormats.Excel1900:
                // Excel with base 1900: Adjust by -1 if v < 61
                v = v < 61 ? v - 1 : v;

                // Date starts at 1/1/1900 = 1
                if (Math.Truncate(v) < 1)
                {
                    throw new ArgumentOutOfRangeException("Not a valid Excel date.", innerException: null);
                }

                return v;
            default:
                // OLE Automation date: Return as is.
                return v;
        }
    }

    /// <summary>
    /// Returns the week of the year for the given first week rule and first day of the
    /// week. When the combination amounts to ISO 8601 (first four-day week, Monday) the
    /// calculation goes through ISOWeek, because Calendar.GetWeekOfYear misnumbers the
    /// year boundary (e.g. 2012-12-31 -> 53 where ISO 8601 says week 1). Shared between
    /// the search results and the Clock dock band so both always show the same number.
    /// </summary>
    /// <param name="date">Date to get the week number for.</param>
    /// <param name="firstWeekRule">Rule for the first week of the year.</param>
    /// <param name="firstDayOfTheWeek">First day of the week.</param>
    /// <returns>The week number.</returns>
    internal static int GetWeekOfYear(DateTime date, CalendarWeekRule firstWeekRule, DayOfWeek firstDayOfTheWeek)
    {
        if (firstWeekRule == CalendarWeekRule.FirstFourDayWeek && firstDayOfTheWeek == DayOfWeek.Monday)
        {
            return ISOWeek.GetWeekOfYear(date);
        }

        return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, firstWeekRule, firstDayOfTheWeek);
    }

    /// <summary>
    /// Returns the week of the year based on the configured 'First week of the year' and
    /// 'First day of the week' settings.
    /// </summary>
    /// <param name="date">Date to get the week number for.</param>
    /// <param name="settings">Extension settings.</param>
    /// <returns>The week number.</returns>
    internal static int GetWeekOfYear(DateTime date, ISettingsInterface settings)
    {
        return GetWeekOfYear(date, GetCalendarWeekRule(settings.FirstWeekOfYear), GetFirstDayOfWeek(settings.FirstDayOfWeek));
    }

    /// <summary>
    /// Returns a CalendarWeekRule enum value based on the configured setting.
    /// </summary>
    internal static CalendarWeekRule GetCalendarWeekRule(int setting)
    {
        switch (setting)
        {
            case 0:
                return CalendarWeekRule.FirstDay;
            case 1:
                return CalendarWeekRule.FirstFullWeek;
            case 2:
                return CalendarWeekRule.FirstFourDayWeek;
            default:
                // Wrong json value and system setting (-1).
                return DateTimeFormatInfo.CurrentInfo.CalendarWeekRule;
        }
    }

    /// <summary>
    /// Returns a DayOfWeek enum value based on the configured FirstDayOfWeek setting.
    /// </summary>
    internal static DayOfWeek GetFirstDayOfWeek(int setting)
    {
        switch (setting)
        {
            case 0:
                return DayOfWeek.Sunday;
            case 1:
                return DayOfWeek.Monday;
            case 2:
                return DayOfWeek.Tuesday;
            case 3:
                return DayOfWeek.Wednesday;
            case 4:
                return DayOfWeek.Thursday;
            case 5:
                return DayOfWeek.Friday;
            case 6:
                return DayOfWeek.Saturday;
            default:
                // Wrong json value and system setting (-1).
                return DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek;
        }
    }
}

/// <summary>
/// Type of time/date format
/// </summary>
internal enum FormatStringType
{
    Time,
    Date,
    DateTime,
}

/// <summary>
/// Different versions of Date formats based on OLE Automation date
/// </summary>
internal enum OADateFormats
{
    OLEAutomation,
    Excel1900,
    Excel1904,
}
