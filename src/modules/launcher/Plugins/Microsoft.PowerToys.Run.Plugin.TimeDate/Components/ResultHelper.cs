// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Wox.Plugin.Logger;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class ResultHelper
    {
        /// <summary>
        /// Returns a list with all available date time formats
        /// </summary>
        /// <param name="isKeywordSearch">Is this a search with plugin activation keyword or not</param>
        /// <param name="timeLong">Required for UnitTest: Show time in long format</param>
        /// <param name="dateLong">Required for UnitTest: Show date in long format</param>
        /// <param name="timestamp">Required for UnitTest: Use custom <see cref="DateTime"/> object to calculate results</param>
        /// <returns>List of results</returns>
        internal static List<AvailableResult> GetAvailableResults(bool isKeywordSearch, bool? timeLong = null, bool? dateLong = null, DateTime? timestamp = null)
        {
            List<AvailableResult> results = new List<AvailableResult>();
            bool timeExtended = timeLong ?? TimeDateSettings.Instance.TimeWithSeconds;
            bool dateExtended = dateLong ?? TimeDateSettings.Instance.DateWithWeekday;
            DateTime dateTimeNow = timestamp ?? DateTime.Now;
            DateTime dateTimeNowUtc = dateTimeNow.ToUniversalTime();
            Calendar calendar = CultureInfo.CurrentCulture.Calendar;

            results.AddRange(new[]
            {
                // This range is reserved for the following three results: Time, Date, Now
                // Don't add any new result in this range! For new results, please use the next range.
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_Time,
                    IconType = ResultIconType.Time,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Date, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_Date,
                    IconType = ResultIconType.Date,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.DateTime, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                    Label = timestamp == null ? Resources.Microsoft_plugin_timedate_Now : Resources.Microsoft_plugin_timedate_DateAndTime,
                    IconType = ResultIconType.DateTime,
                },
            });

            if (isKeywordSearch || !TimeDateSettings.Instance.OnlyDateTimeNowGlobal)
            {
                // We use long instead of int  for unix time stamp because int ist to small after 03:14:07 UTC 2038-01-19
                long unixTimestamp = (long)dateTimeNowUtc.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                int weekOfYear = calendar.GetWeekOfYear(dateTimeNow, DateTimeFormatInfo.CurrentInfo.CalendarWeekRule, DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);

                results.AddRange(new[]
                {
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_TimeUtc,
                        IconType = ResultIconType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.DateTime, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                        Label = timestamp == null ? Resources.Microsoft_plugin_timedate_NowUtc : Resources.Microsoft_plugin_timedate_DateAndTimeUtc,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = unixTimestamp.ToString(CultureInfo.CurrentCulture),
                        Label = timestamp == null ? Resources.Microsoft_plugin_timedate_UnixNow : Resources.Microsoft_plugin_timedate_Unix,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = DateTimeFormatInfo.CurrentInfo.GetDayName(dateTimeNow.DayOfWeek),
                        Label = Resources.Microsoft_plugin_timedate_Day,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = TimeAndDateHelper.GetNumberOfDayInWeek(dateTimeNow).ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayOfWeek,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Day.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayOfMonth,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.DayOfYear.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayOfYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = TimeAndDateHelper.GetWeekOfMonth(dateTimeNow).ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfMonth,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = weekOfYear.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = DateTimeFormatInfo.CurrentInfo.GetMonthName(dateTimeNow.Month),
                        Label = Resources.Microsoft_plugin_timedate_Month,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Month.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_MonthOfYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("M", CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayMonth,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = calendar.GetYear(dateTimeNow).ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Year,
                        IconType = ResultIconType.Date,
                    },
                    TimeAndDateHelper.GetCalendarEraResult(dateTimeNow, calendar),
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("Y", CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_MonthYear,
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Ticks.ToString(CultureInfo.CurrentCulture),
                        Label = timestamp == null ? Resources.Microsoft_plugin_timedate_WindowsFileTimeNow : Resources.Microsoft_plugin_timedate_WindowsFileTime,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("u"),
                        Label = Resources.Microsoft_plugin_timedate_UniversalTime,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("s"),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601Utc,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601Zone,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601ZoneUtc,
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("R"),
                        Label = Resources.Microsoft_plugin_timedate_Rfc1123,
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
