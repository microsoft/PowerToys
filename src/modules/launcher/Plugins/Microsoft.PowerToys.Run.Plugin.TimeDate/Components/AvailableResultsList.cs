// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal class AvailableResultsList
    {
        /// <summary>
        /// Returns a list with all available date time formats
        /// </summary>
        /// <param name="isKeywordSearch">Is this a search with plugin activation keyword or not</param>
        /// <param name="timeLong">Required for UnitTest: Show time in long format</param>
        /// <param name="dateLong">Required for UnitTest: Show date in long format</param>
        /// <param name="timestamp">Required for UnitTest: Use custom <see cref="DateTime"/> object to calculate results</param>
        /// <returns>List of results</returns>
        internal static List<AvailableResult> GetList(bool isKeywordSearch, bool? timeLong = null, bool? dateLong = null, DateTime? timestamp = null)
        {
            List<AvailableResult> results = new List<AvailableResult>();
            bool timeExtended = timeLong ?? TimeDateSettings.Instance.TimeWithSeconds;
            bool dateExtended = dateLong ?? TimeDateSettings.Instance.DateWithWeekday;
            bool isSystemDateTime = timestamp == null;
            Calendar calendar = CultureInfo.CurrentCulture.Calendar;
            DateTime dateTimeNow = timestamp ?? DateTime.Now;
            DateTime dateTimeNowUtc = dateTimeNow.ToUniversalTime();

            results.AddRange(new[]
            {
                // This range is reserved for the following three results: Time, Date, Now
                // Don't add any new result in this range! For new results, please use the next range.
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_Time,
                    AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, string.Empty, "Microsoft_plugin_timedate_SearchTagTimeNow"),
                    IconType = ResultIconType.Time,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Date, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_Date,
                    AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, string.Empty, "Microsoft_plugin_timedate_SearchTagDateNow"),
                    IconType = ResultIconType.Date,
                },
                new AvailableResult()
                {
                    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.DateTime, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                    Label = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_DateAndTime", "Microsoft_plugin_timedate_Now"),
                    AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                    IconType = ResultIconType.DateTime,
                },
            });

            if (isKeywordSearch || !TimeDateSettings.Instance.OnlyDateTimeNowGlobal)
            {
                // We use long instead of int for unix time stamp because int is too small after 03:14:07 UTC 2038-01-19
                long unixTimestamp = ((DateTimeOffset)dateTimeNowUtc).ToUnixTimeSeconds();
                long unixTimestampMilliseconds = ((DateTimeOffset)dateTimeNowUtc).ToUnixTimeMilliseconds();
                int weekOfYear = calendar.GetWeekOfYear(dateTimeNow, DateTimeFormatInfo.CurrentInfo.CalendarWeekRule, DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);
                string era = DateTimeFormatInfo.CurrentInfo.GetEraName(calendar.GetEra(dateTimeNow));
                string eraShort = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(calendar.GetEra(dateTimeNow));

                results.AddRange(new[]
                {
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_TimeUtc,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, string.Empty, "Microsoft_plugin_timedate_SearchTagTimeNow"),
                        IconType = ResultIconType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.DateTime, timeExtended, dateExtended), CultureInfo.CurrentCulture),
                        Label = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_DateAndTimeUtc", "Microsoft_plugin_timedate_NowUtc"),
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = unixTimestamp.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Unix,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = unixTimestampMilliseconds.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Unix_Milliseconds,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Hour.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Hour,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagTime"),
                        IconType = ResultIconType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Minute.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Minute,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagTime"),
                        IconType = ResultIconType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Second.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Second,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagTime"),
                        IconType = ResultIconType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Millisecond.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Millisecond,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagTime"),
                        IconType = ResultIconType.Time,
                    },
                    new AvailableResult()
                    {
                        Value = DateTimeFormatInfo.CurrentInfo.GetDayName(dateTimeNow.DayOfWeek),
                        Label = Resources.Microsoft_plugin_timedate_Day,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = TimeAndDateHelper.GetNumberOfDayInWeek(dateTimeNow).ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayOfWeek,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Day.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayOfMonth,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.DayOfYear.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayOfYear,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = TimeAndDateHelper.GetWeekOfMonth(dateTimeNow).ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfMonth,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = weekOfYear.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_WeekOfYear,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = DateTimeFormatInfo.CurrentInfo.GetMonthName(dateTimeNow.Month),
                        Label = Resources.Microsoft_plugin_timedate_Month,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.Month.ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_MonthOfYear,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("M", CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_DayMonth,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = calendar.GetYear(dateTimeNow).ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_Year,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = era,
                        Label = Resources.Microsoft_plugin_timedate_Era,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagEra"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = era != eraShort ? eraShort : string.Empty, // Setting value to empty string if 'era == eraShort'. This result will be filtered later.
                        Label = Resources.Microsoft_plugin_timedate_EraAbbreviation,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagEra"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("Y", CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_MonthYear,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                        IconType = ResultIconType.Date,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToFileTime().ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_WindowsFileTime,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("u"),
                        Label = Resources.Microsoft_plugin_timedate_UniversalTime,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("s"),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601Utc,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601Zone,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNowUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_Iso8601ZoneUtc,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("R"),
                        Label = Resources.Microsoft_plugin_timedate_Rfc1123,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                    new AvailableResult()
                    {
                        Value = dateTimeNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture),
                        Label = Resources.Microsoft_plugin_timedate_filename_compatible,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    },
                });
            }

            // Return only results where value is not empty
            // This can happen, for example, when we can't read the 'era' or when 'era == era abbreviation' and we set value explicitly to an empty string.
            return results.Where(x => !string.IsNullOrEmpty(x.Value)).ToList();
        }
    }
}
