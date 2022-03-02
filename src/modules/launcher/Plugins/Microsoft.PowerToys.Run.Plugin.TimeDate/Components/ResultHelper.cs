// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class ResultHelper
    {
        /// <summary>
        /// Returns a list with all available commands
        /// </summary>
        /// <param name="isKeywordSearch">Is this a search with plugin activation keyword or not</param>
        /// <param name="timeLong">Required for UnitTest: Show time in long format</param>
        /// <param name="dateLong">Required for UnitTest: Show date in long format</param>
        /// <param name="timestamp">Required for UnitTest: Use custom <see cref="DateTime"/> object to calculate results</param>
        /// <returns>List of results</returns>
        internal static List<AvailableResult> GetCommandList(bool isKeywordSearch, bool? timeLong = null, bool? dateLong = null, DateTime? timestamp = null)
        {
            List<AvailableResult> results = new List<AvailableResult>();
            DateTime dateTimeNow = timestamp ?? DateTime.Now;
            bool timeExtended = timeLong ?? TimeDateSettings.Instance.TimeWithSeconds;
            bool dateExtended = dateLong ?? TimeDateSettings.Instance.DateWithWeekday;

            results.AddRange(new[]
            {
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(TimestampType.Time, timeExtended, dateExtended)),
                    Label = Resources.Microsoft_plugin_timedate_time,
                    Type = TimestampType.Time,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(TimestampType.Date, timeExtended, dateExtended)),
                    Label = Resources.Microsoft_plugin_timedate_date,
                    Type = TimestampType.Date,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(TimestampType.DateTime, timeExtended, dateExtended)),
                    Label = Resources.Microsoft_plugin_timedate_now,
                    Type = TimestampType.DateTime,
                },
            });

            if (isKeywordSearch || !TimeDateSettings.Instance.OnlyDateTimeNowGlobal)
            {
                long unixTimestamp = (long)dateTimeNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                int weekOfMonth = TimeAndDateHelper.GetWeekOfMonth(dateTimeNow);
                int weekOfYear = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(dateTimeNow, CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

                results.AddRange(new[]
                {
                    new AvailableResult()
                    {
                        Value = unixTimestamp.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_timeUnix,
                        Type = TimestampType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dateTimeNow.DayOfWeek),
                        Label = Resources.Microsoft_plugin_timedate_Day,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = ((int)dateTimeNow.DayOfWeek).ToString(),
                        Label = Resources.Microsoft_plugin_timedate_DayOfWeek,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Day.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_DayOfMonth,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.DayOfYear.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_DayOfYear,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = weekOfMonth.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfMonth,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = weekOfYear.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfYear,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTimeNow.Month),
                        Label = Resources.Microsoft_plugin_timedate_Month,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Month.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_MonthOfYear,
                        Type = TimestampType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Year.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_Year,
                        Type = TimestampType.Date,
                    },
                });
            }

            return results;
        }

        /// <summary>
        /// Copy the given text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to the clipboard</param>
        /// <returns><see langword="true"/>The text successful copy to the clipboard, otherwise <see langword="false"/></returns>
        /// <remarks>Code copied from TimeZone plugin</remarks>
        internal static bool CopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(ResultHelper));
                return false;
            }
        }
    }
}
