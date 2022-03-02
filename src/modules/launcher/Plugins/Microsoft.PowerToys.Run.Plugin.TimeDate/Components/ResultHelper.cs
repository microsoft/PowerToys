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
    public static class ResultHelper
    {
        /// <summary>
        /// Returns a list with all available commands
        /// </summary>
        /// <param name="isKeywordSearch">Is this a search with plugin activation keyword or not</param>
        /// <param name="timeLong">Required for UnitTest: Show time in long format</param>
        /// <param name="dateLong">Required for UnitTest: Show date in long format</param>
        /// <param name="timestamp">Required for UnitTest: Use custom <see cref="DateTime"/> object to calculate results</param>
        /// <returns>List of results</returns>
        public static List<AvailableResult> GetCommandList(bool isKeywordSearch, bool? timeLong = null, bool? dateLong = null, DateTime? timestamp = null)
        {
            List<AvailableResult> results = new List<AvailableResult>();
            DateTime dateTimeNow = timestamp ?? DateTime.Now;
            DateTime dateTimeNowUtc = dateTimeNow.ToUniversalTime();
            bool timeExtended = timeLong ?? TimeDateSettings.Instance.TimeWithSeconds;
            bool dateExtended = dateLong ?? TimeDateSettings.Instance.DateWithWeekday;

            results.AddRange(new[]
            {
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended)),
                    Label = Resources.Microsoft_plugin_timedate_time,
                    IconType = ResultIconType.Time,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Date, timeExtended, dateExtended)),
                    Label = Resources.Microsoft_plugin_timedate_date,
                    IconType = ResultIconType.Date,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.DateTime, timeExtended, dateExtended)),
                    Label = timestamp == null ? Resources.Microsoft_plugin_timedate_Now : Resources.Microsoft_plugin_timedate_DateAndTime,
                    IconType = ResultIconType.DateTime,
                },
            });

            if (isKeywordSearch || !TimeDateSettings.Instance.OnlyDateTimeNowGlobal)
            {
                long unixTimestamp = (long)dateTimeNowUtc.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                int weekOfMonth = TimeAndDateHelper.GetWeekOfMonth(dateTimeNow);
                int weekOfYear = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(dateTimeNow, CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

                results.AddRange(new[]
                {
                    new AvailableResult()
                    {
                        Value = unixTimestamp.ToString(),
                        Label = timestamp == null ? Resources.Microsoft_plugin_timedate_unixNow : Resources.Microsoft_plugin_timedate_unix,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dateTimeNow.DayOfWeek),
                        Label = Resources.Microsoft_plugin_timedate_Day,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = ((int)dateTimeNow.DayOfWeek).ToString(),
                        Label = Resources.Microsoft_plugin_timedate_DayOfWeek,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Day.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_DayOfMonth,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.DayOfYear.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_DayOfYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = weekOfMonth.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfMonth,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = weekOfYear.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTimeNow.Month),
                        Label = Resources.Microsoft_plugin_timedate_Month,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Month.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_MonthOfYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Year.ToString(),
                        Label = Resources.Microsoft_plugin_timedate_Year,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Ticks.ToString(),
                        Label = timestamp == null ? Resources.Microsoft_plugin_timedate_WindowsFileTimeNow : Resources.Microsoft_plugin_timedate_WindowsFileTime,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("u"),
                        Label = "Universal time format (YYYY-MM-DD hh:mm:ss)",
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("s"),
                        Label = "ISO 8601 (Timestamp)",
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Label = "ISO 8601 UTC (Timestamp)",
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        Label = "ISO 8601 with timezone (Timestamp)",
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'"),
                        Label = "ISO 8601 UTC with timezone (Timestamp)",
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("R"),
                        Label = "RFC1123 (Date and time)",
                        IconType = ResultIconType.DateTime,
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
