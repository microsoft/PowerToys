// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Wox.Plugin.Logger;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class TimeAndDateHelper
    {
        private static readonly Regex _regexSpecialInputFormats = new Regex(@"^.*(u|ums|ft|oa|exc|exf)\d");
        private static readonly Regex _regexCustomDateTimeFormats = new Regex(@"(?<!\\)(DOW|WOM|WOY|ELF|WFT|UXT|UXMS|OAD|EXC|EXF)");

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
            DateTime beginningOfMonth = new DateTime(date.Year, date.Month, 1);
            int adjustment = 1; // We count from 1 to 7 and not from 0 to 6

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
            int daysInWeek = 7;
            int adjustment = 1; // We count from 1 to 7 and not from 0 to 6

            return ((date.DayOfWeek + daysInWeek - formatSettingFirstDayOfWeek) % daysInWeek) + adjustment;
        }

        internal static double ConvertToOleAutomationFormat(OADateFormats type, DateTime date)
        {
            throw new NotImplementedException();
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
            else if (Regex.IsMatch(input, @"^u[\+-]?\d{1,10}$") && long.TryParse(input.TrimStart('u'), out long secondsU))
            {
                // Unix time stamp
                // We use long instead of int, because int is too small after 03:14:07 UTC 2038-01-19
                timestamp = DateTimeOffset.FromUnixTimeSeconds(secondsU).LocalDateTime;
                return true;
            }
            else if (Regex.IsMatch(input, @"^ums[\+-]?\d{1,13}$") && long.TryParse(input.TrimStart("ums".ToCharArray()), out long millisecondsUms))
            {
                // Unix time stamp in milliseconds
                // We use long instead of int because int is too small after 03:14:07 UTC 2038-01-19
                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(millisecondsUms).LocalDateTime;
                return true;
            }
            else if (Regex.IsMatch(input, @"^ft\d+$") && long.TryParse(input.TrimStart("ft".ToCharArray()), out long secondsFt))
            {
                // Windows file time
                // DateTime.FromFileTime returns as local time.
                timestamp = DateTime.FromFileTime(secondsFt);
                return true;
            }
            else if (Regex.IsMatch(input, @"^oa-?\d+[,.0-9]*$") && double.TryParse(input.TrimStart("oa".ToCharArray()), out double oADate))
            {
                // OLE Automation date
                // Input has to be in the range from -657434.99999999 to 2958465.99999999
                // DateTime.FromOADate returns as local time.
                if (oADate < -657434.99999999 || oADate > 2958465.99999999)
                {
                    Log.Error($"Input for OLE Automation date does not fall within the range from -657434.99999999 to 2958465.99999999: {oADate}", typeof(TimeAndDateHelper));
                    timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                    return false;
                }

                timestamp = DateTime.FromOADate(oADate);
                return true;
            }
            else if (Regex.IsMatch(input, @"^exc\d+[,.0-9]*$") && double.TryParse(input.TrimStart("exc".ToCharArray()), out double excDate))
            {
                // Excel's 1900 date value
                // Input has to be in the range from 1 to 2958465.99998843 and not 60 whole number
                // Because of a bug in Excel and the way it behaves before 3/1/1900 we have to adjust all inputs lower than 61 for +1
                // DateTime.FromOADate returns as local time.
                if (excDate < 0 || excDate > 2958465.99998843)
                {
                    Log.Error($"Input for Excel's 1900 date value does not fall within the range from 0 to 2958465.99998843: {excDate}", typeof(TimeAndDateHelper));
                    timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                    return false;
                }

                if (Math.Truncate(excDate) == 0 || Math.Truncate(excDate) == 60)
                {
                    Log.Error($"Cannot parse {excDate} as Excel's 1900 date value because it is a fake date. (In Excel 0 stands for 0/1/1900 and this date doesn't exist. And 60 stands for 2/29/1900 and this date only exists in Excel for compatibility with Lotus 123.)", typeof(TimeAndDateHelper));
                    timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                    return false;
                }

                excDate = excDate <= 60 ? excDate + 1 : excDate;
                timestamp = DateTime.FromOADate(excDate);
                return true;
            }
            else if (Regex.IsMatch(input, @"^exf\d+[,.0-9]*$") && double.TryParse(input.TrimStart("exf".ToCharArray()), out double exfDate))
            {
                // Excel's 1904 date value
                // Input has to be in the range from 0 to 2957003.99998843
                // Because Excel uses 01/01/1904 as base we need to adjust for +1462
                // DateTime.FromOADate returns as local time.
                if (exfDate < 0 || exfDate > 2957003.99998843)
                {
                    Log.Error($"Input for Excel's 1904 date value does not fall within the range from 0 to 2957003.99998843: {exfDate}", typeof(TimeAndDateHelper));
                    timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                    return false;
                }

                timestamp = DateTime.FromOADate(exfDate + 1462);
                return true;
            }
            else
            {
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                return false;
            }
        }

        /// <summary>
        /// Test if input is special parsing for Unix time, Unix time in milliseconds, file time, ...
        /// </summary>
        /// <param name="input">String with date/time</param>
        /// <returns>True if yes, otherwise false</returns>
        internal static bool IsSpecialInputParsing(string input)
        {
            return _regexSpecialInputFormats.IsMatch(input);
        }

        /// <summary>
        /// Converts a DateTime object based on the format string
        /// </summary>
        /// <param name="date">Date/time object.</param>
        /// <param name="unix">Value for replacing "Unix Time Stamp".</param>
        /// <param name="unixMilliseconds">Value for replacing "Unix Time Stamp in milliseconds".</param>
        /// <param name="calWeek">Value for relacing calendar week.</param>
        /// <param name="format">Format definition.</param>
        /// <returns>Formated date/time string.</returns>
        internal static string ConvertToCustomFormat(DateTime date, long unix, long unixMilliseconds, int calWeek, string eraLongFormat, string format)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Test a string for our custom date and time format syntax
        /// </summary>
        /// <param name="str">String to test.</param>
        /// <returns>True if yes and otherwise false</returns>
        internal static bool StringContainsCustomFormatSyntax(string str)
        {
            return _regexCustomDateTimeFormats.IsMatch(str);
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

    /// <summary>
    /// Differnet versions of Date formats based on OLE Automation date
    /// </summary>
    internal enum OADateFormats
    {
        OLEAutomation,
        Excle1900,
        Excel1904,
    }
}
