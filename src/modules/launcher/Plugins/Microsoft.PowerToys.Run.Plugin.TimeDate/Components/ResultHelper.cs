// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class ResultHelper
    {
        /// <summary>
        /// Returns a list with all available commands
        /// </summary>
        /// <param name="iconTheme">Them for the icon</param>
        /// <returns>List of results</returns>
        internal static List<Result> GetCommandList(bool isKeywordSearch, string iconTheme)
        {
            List<Result> results = new List<Result>();
            DateTime dateTimeNow = DateTime.Now;

            results.AddRange(new[]
            {
                new Result()
                {
                    Title = dateTimeNow.ToString(GetStringFormat(FormatType.Time)),
                    SubTitle = $"{Resources.Microsoft_plugin_timedate_time} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                    IcoPath = $"Images\\time.{iconTheme}.png",
                    Action = _ => TryToCopyToClipBoard(dateTimeNow.ToString(GetStringFormat(FormatType.Time))),
                    ContextData = Resources.Microsoft_plugin_timedate_time, // Search term
                },
                new Result()
                {
                    Title = dateTimeNow.ToString(GetStringFormat(FormatType.Date)),
                    SubTitle = $"{Resources.Microsoft_plugin_timedate_date} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                    IcoPath = $"Images\\calendar.{iconTheme}.png",
                    Action = _ => TryToCopyToClipBoard(dateTimeNow.ToString(GetStringFormat(FormatType.Date))),
                    ContextData = Resources.Microsoft_plugin_timedate_date, // Search term
                },
                new Result()
                {
                    Title = dateTimeNow.ToString(GetStringFormat(FormatType.DateTime)),
                    SubTitle = $"{Resources.Microsoft_plugin_timedate_now} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                    IcoPath = $"Images\\timeDate.{iconTheme}.png",
                    Action = _ => TryToCopyToClipBoard(dateTimeNow.ToString(GetStringFormat(FormatType.DateTime))),
                    ContextData = Resources.Microsoft_plugin_timedate_now, // Search term
                },
            });

            if (isKeywordSearch || !TimeDateSettings.Instance.DateTimeNowGlobalOnly)
            {
                long unixTimestamp = (long)dateTimeNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                int weekOfMonth = GetWeekOfMonth(dateTimeNow);
                int weekOfYear = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(dateTimeNow, CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

                results.AddRange(new[]
                {
                    new Result()
                    {
                        Title = unixTimestamp.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_timeUnix} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\time.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(unixTimestamp.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_timeUnix, // Search term
                    },
                    new Result()
                    {
                        Title = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dateTimeNow.DayOfWeek),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_Day} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dateTimeNow.DayOfWeek)),
                        ContextData = Resources.Microsoft_plugin_timedate_Day, // Search term
                    },
                    new Result()
                    {
                        Title = ((int)dateTimeNow.DayOfWeek).ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_DayOfWeek} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(((int)dateTimeNow.DayOfWeek).ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_DayOfWeek, // Search term
                    },
                    new Result()
                    {
                        Title = dateTimeNow.Day.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_DayOfMonth} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(dateTimeNow.Day.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_DayOfMonth, // Search term
                    },
                    new Result()
                    {
                        Title = dateTimeNow.DayOfYear.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_DayOfYear} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(dateTimeNow.DayOfYear.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_DayOfYear, // Search term
                    },
                    new Result()
                    {
                        Title = weekOfMonth.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_WeekOfMonth} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(weekOfMonth.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_WeekOfMonth, // Search term
                    },
                    new Result()
                    {
                        Title = weekOfYear.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_WeekOfYear} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(weekOfYear.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_WeekOfYear, // Search term
                    },
                    new Result()
                    {
                        Title = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTimeNow.Month),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_Month} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTimeNow.Month)),
                        ContextData = Resources.Microsoft_plugin_timedate_Month, // Search term
                    },
                    new Result()
                    {
                        Title = dateTimeNow.Month.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_MonthOfYear} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(dateTimeNow.Month.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_MonthOfYear, // Search term
                    },
                    new Result()
                    {
                        Title = dateTimeNow.Year.ToString(),
                        SubTitle = $"{Resources.Microsoft_plugin_timedate_Year} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = $"Images\\calendar.{iconTheme}.png",
                        Action = _ => TryToCopyToClipBoard(dateTimeNow.Year.ToString()),
                        ContextData = Resources.Microsoft_plugin_timedate_Year, // Search term
                    },
                });
            }

            return results;
        }

        /// <summary>
        /// Get the format for the time string
        /// </summary>
        /// <param name="targetFormat">Type of format</param>
        /// <returns>String that identifies the time/date format (<see href="https://docs.microsoft.com/en-us/dotnet/api/system.datetime.tostring"/>)</returns>
        private static string GetStringFormat(FormatType targetFormat)
        {
            switch (targetFormat)
            {
                case FormatType.Time:
                    return TimeDateSettings.Instance.TimeWithSeconds ? "T" : "t";
                case FormatType.Date:
                    return TimeDateSettings.Instance.DateWithWeekday ? "D" : "d";
                case FormatType.DateTime:
                    if (TimeDateSettings.Instance.TimeWithSeconds & TimeDateSettings.Instance.DateWithWeekday)
                    {
                        return "F"; // Friday, October 31, 2008 5:04:32 PM
                    }
                    else if (TimeDateSettings.Instance.TimeWithSeconds & !TimeDateSettings.Instance.DateWithWeekday)
                    {
                        return "G"; // 10/31/2008 5:04:32 PM
                    }
                    else if (!TimeDateSettings.Instance.TimeWithSeconds & TimeDateSettings.Instance.DateWithWeekday)
                    {
                        return "f"; // Friday, October 31, 2008 5:04 PM
                    }
                    else
                    {
                        // (!TimeDateSettings.Instance.TimeWithSeconds & !TimeDateSettings.Instance.DateWithWeekday)
                        return "g"; // 10/31/2008 5:04 PM
                    }

                default:
                    return string.Empty; // Windows default based on current culture settings
            }
        }

        public static int GetWeekOfMonth(DateTime date)
        {
            DateTime beginningOfMonth = new DateTime(date.Year, date.Month, 1);

            while (date.Date.AddDays(1).DayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
            {
                date = date.AddDays(1);
            }

            return (int)Math.Truncate((double)date.Subtract(beginningOfMonth).TotalDays / 7f) + 1;
        }

        /// <summary>
        /// Copy the given text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to the clipboard</param>
        /// <returns><see langword="true"/>The text successful copy to the clipboard, otherwise <see langword="false"/></returns>
        /// <remarks>Code copied from TimeZone plugin</remarks>
        private static bool TryToCopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(Main));
                return false;
            }
        }

        /// <summary>
        /// Type of time format
        /// </summary>
        private enum FormatType
        {
            Time,
            Date,
            DateTime,
        }
    }
}
