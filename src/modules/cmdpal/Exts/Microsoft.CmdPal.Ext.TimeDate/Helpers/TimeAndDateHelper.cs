// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

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
    /// <returns>Number of week in the month</returns>
    internal static int GetWeekOfMonth(DateTime date, DayOfWeek formatSettingFirstDayOfWeek)
    {
        var beginningOfMonth = new DateTime(date.Year, date.Month, 1);
        var adjustment = 1; // We count from 1 to 7 and not from 0 to 6

        while (date.Date.AddDays(1).DayOfWeek != formatSettingFirstDayOfWeek)
        {
            date = date.AddDays(1);
        }

        return (int)Math.Truncate((double)date.Subtract(beginningOfMonth).TotalDays / 7f) + adjustment;
    }

    /// <summary>
    /// Returns the number of the day in the week
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Number of the day in the week</returns>
    internal static int GetNumberOfDayInWeek(DateTime date, DayOfWeek formatSettingFirstDayOfWeek)
    {
        var daysInWeek = 7;
        var adjustment = 1; // We count from 1 to 7 and not from 0 to 6

        return ((date.DayOfWeek + daysInWeek - formatSettingFirstDayOfWeek) % daysInWeek) + adjustment;
    }

    /// <summary>
    /// Convert input string to a <see cref="DateTime"/> object in local time
    /// </summary>
    /// <param name="input">String with date/time</param>
    /// <param name="timestamp">The new <see cref="DateTime"/> object</param>
    /// <returns>True on success, otherwise false</returns>
    internal static bool ParseStringAsDateTime(in string input, out DateTime timestamp)
    {
        if (DateTime.TryParse(input, out timestamp))
        {
            // Known date/time format
            return true;
        }
        else if (Regex.IsMatch(input, @"^u[\+-]?\d{1,10}$") && long.TryParse(input.TrimStart('u'), out var secondsU))
        {
            // Unix time stamp
            // We use long instead of int, because int is too small after 03:14:07 UTC 2038-01-19
            timestamp = DateTimeOffset.FromUnixTimeSeconds(secondsU).LocalDateTime;
            return true;
        }
        else if (Regex.IsMatch(input, @"^ums[\+-]?\d{1,13}$") && long.TryParse(input.TrimStart("ums".ToCharArray()), out var millisecondsUms))
        {
            // Unix time stamp in milliseconds
            // We use long instead of int because int is too small after 03:14:07 UTC 2038-01-19
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(millisecondsUms).LocalDateTime;
            return true;
        }
        else if (Regex.IsMatch(input, @"^ft\d+$") && long.TryParse(input.TrimStart("ft".ToCharArray()), out var secondsFt))
        {
            // Windows file time
            // DateTime.FromFileTime returns as local time.
            timestamp = DateTime.FromFileTime(secondsFt);
            return true;
        }
        else
        {
            timestamp = new DateTime(1, 1, 1, 1, 1, 1);
            return false;
        }
    }

    /// <summary>
    /// Test if input is special parsing for Unix time, Unix time in milliseconds or File time.
    /// </summary>
    /// <param name="input">String with date/time</param>
    /// <returns>True if yes, otherwise false</returns>
    internal static bool IsSpecialInputParsing(string input)
    {
        return Regex.IsMatch(input, @"^.*(u|ums|ft)\d");
    }

    /// <summary>
    /// Returns a CalendarWeekRule enum value based on the plugin setting.
    /// </summary>
    internal static CalendarWeekRule GetCalendarWeekRule(int pluginSetting)
    {
        switch (pluginSetting)
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
    /// Returns a DayOfWeek enum value based on the FirstDayOfWeek plugin setting.
    /// </summary>
    internal static DayOfWeek GetFirstDayOfWeek(int pluginSetting)
    {
        switch (pluginSetting)
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
