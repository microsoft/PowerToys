// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

internal static class AvailableResultsList
{
    /// <summary>
    /// Returns a list with all available date time formats
    /// </summary>
    /// <param name="timeLongFormat">Required for UnitTest: Show time in long format</param>
    /// <param name="dateLongFormat">Required for UnitTest: Show date in long format</param>
    /// <param name="timestamp">Use custom <see cref="DateTime"/> object to calculate results instead of the system date/time</param>
    /// <param name="firstWeekOfYear">Required for UnitTest: Use custom first week of the year instead of the configured setting.</param>
    /// <param name="firstDayOfWeek">Required for UnitTest: Use custom first day of the week instead of the configured setting.</param>
    /// <returns>List of results</returns>
    internal static List<AvailableResult> GetList(bool isKeywordSearch, ISettingsInterface settings, bool? timeLongFormat = null, bool? dateLongFormat = null, DateTime? timestamp = null, CalendarWeekRule? firstWeekOfYear = null, DayOfWeek? firstDayOfWeek = null, DateTimeOffset? currentTime = null)
    {
        var results = new List<AvailableResult>();
        var calendar = CultureInfo.CurrentCulture.Calendar;

        var timeExtended = timeLongFormat ?? settings.TimeWithSecond;
        var dateExtended = dateLongFormat ?? settings.DateWithWeekday;
        var isSystemDateTime = timestamp is null && currentTime is null;
        var dateTimeNow = timestamp ?? currentTime?.DateTime ?? DateTime.Now;
        var dateTimeNowUtc = currentTime?.UtcDateTime ?? dateTimeNow.ToUniversalTime();
        var firstWeekRule = firstWeekOfYear ?? TimeAndDateHelper.GetCalendarWeekRule(settings.FirstWeekOfYear);
        var firstDayOfTheWeek = firstDayOfWeek ?? TimeAndDateHelper.GetFirstDayOfWeek(settings.FirstDayOfWeek);

        // Shared with the Clock dock band; corrects the year boundary via ISOWeek
        // when the settings amount to ISO 8601 (first four-day week + Monday).
        var weekOfYear = TimeAndDateHelper.GetWeekOfYear(dateTimeNow, firstWeekRule, firstDayOfTheWeek);

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
            new AvailableResult()
            {
                Value = weekOfYear.ToString(CultureInfo.CurrentCulture),
                Label = Resources.Microsoft_plugin_timedate_WeekOfYear,
                AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagDate"),
                IconType = ResultIconType.Date,
            },
        });

        if (isKeywordSearch)
        {
            // We use long instead of int for unix time stamp because int is too small after 03:14:07 UTC 2038-01-19
            var unixTimestamp = ((DateTimeOffset)dateTimeNowUtc).ToUnixTimeSeconds();
            var unixTimestampMilliseconds = ((DateTimeOffset)dateTimeNowUtc).ToUnixTimeMilliseconds();
            var era = DateTimeFormatInfo.CurrentInfo.GetEraName(calendar.GetEra(dateTimeNow));
            var eraShort = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(calendar.GetEra(dateTimeNow));

            // Custom formats
            var formatTime = currentTime ?? new DateTimeOffset(dateTimeNow);
            foreach (var f in settings.CustomFormats)
            {
                var formatParts = f.Split("=", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var formatSyntax = formatParts.Length == 2 ? formatParts[1] : string.Empty;
                var searchTags = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagCustom");

                // If Length = 0 then empty string.
                if (formatParts.Length >= 1)
                {
                    try
                    {
                        // Verify and check input and update search tags
                        if (formatParts.Length == 1)
                        {
                            throw new FormatException("Format syntax part after equal sign is missing.");
                        }

                        if (formatSyntax.StartsWith("UTC:", StringComparison.InvariantCulture))
                        {
                            searchTags = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagCustomUtc");
                        }

                        var value = CustomClockDisplay.Format(formatTime, formatSyntax, settings);

                        // Add result
                        results.Add(new AvailableResult()
                        {
                            Value = value,
                            Label = formatParts[0],
                            AlternativeSearchTag = searchTags,
                            IconType = ResultIconType.DateTime,
                        });
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.LogError($"ArgumentOutOfRangeException with format: {formatSyntax}. Error: {e.Message}");
                        results.Add(new AvailableResult()
                        {
                            Value = Resources.Microsoft_plugin_timedate_ErrorConvertCustomFormat,
                            Label = formatParts[0] + " - " + Resources.Microsoft_plugin_timedate_show_details,
                            AlternativeSearchTag = searchTags,
                            IconType = ResultIconType.Error,
                            ErrorDetails = e.Message,
                        });
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception with format: {formatSyntax}. Error: {e.Message}");
                        results.Add(new AvailableResult()
                        {
                            Value = Resources.Microsoft_plugin_timedate_InvalidCustomFormat + " " + formatSyntax,
                            Label = formatParts[0] + " - " + Resources.Microsoft_plugin_timedate_show_details,
                            AlternativeSearchTag = searchTags,
                            IconType = ResultIconType.Error,
                            ErrorDetails = e.Message,
                        });
                    }
                }
            }

            // Predefined formats
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
                    Value = TimeAndDateHelper.GetNumberOfDayInWeek(dateTimeNow, firstDayOfTheWeek).ToString(CultureInfo.CurrentCulture),
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
                    Value = DateTime.DaysInMonth(dateTimeNow.Year, dateTimeNow.Month).ToString(CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_DaysInMonth,
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
                    Value = TimeAndDateHelper.GetWeekOfMonth(dateTimeNow, firstDayOfTheWeek).ToString(CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_WeekOfMonth,
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
                    Value = DateTime.IsLeapYear(dateTimeNow.Year) ? Resources.Microsoft_plugin_timedate_LeapYear : Resources.Microsoft_plugin_timedate_NoLeapYear,
                    Label = Resources.Microsoft_plugin_timedate_LeapYear,
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
            });

            try
            {
                results.Add(new AvailableResult()
                {
                    Value = (currentTime?.ToFileTime() ?? dateTimeNow.ToFileTime()).ToString(CultureInfo.CurrentCulture),
                    Label = Resources.Microsoft_plugin_timedate_WindowsFileTime,
                    AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                    IconType = ResultIconType.DateTime,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unable to convert to Windows file time: {ex.Message}");
                results.Add(new AvailableResult()
                {
                    Value = Resources.Microsoft_plugin_timedate_ErrorConvertWft,
                    Label = Resources.Microsoft_plugin_timedate_WindowsFileTime,
                    AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                    IconType = ResultIconType.Error,
                });
            }

            results.AddRange(new[]
            {
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
                    Value = currentTime?.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture) ?? dateTimeNow.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture),
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
                    Value = currentTime?.ToString("R", CultureInfo.InvariantCulture) ?? dateTimeNow.ToString("R"),
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

    /// <summary>
    /// Returns the results for the difference between two dates ("to - from").
    /// </summary>
    /// <param name="from">The date that is subtracted.</param>
    /// <param name="to">The date that is subtracted from.</param>
    /// <returns>List of results</returns>
    internal static List<AvailableResult> GetDifferenceList(DateTime from, DateTime to)
    {
        const string totalFormat = "#,##0.##";
        var span = to - from;

        return
        [
            new AvailableResult()
            {
                Value = FormatCalendarDifference(from, to),
                Label = Resources.Microsoft_plugin_timedate_Difference,
                AlternativeSearchTag = Resources.Microsoft_plugin_timedate_SearchTagDifference,
                IconType = ResultIconType.Date,
            },
            new AvailableResult()
            {
                Value = span.TotalDays.ToString(totalFormat, CultureInfo.CurrentCulture),
                Label = Resources.Microsoft_plugin_timedate_TotalDays,
                AlternativeSearchTag = Resources.Microsoft_plugin_timedate_SearchTagDifference,
                IconType = ResultIconType.Date,
            },
            new AvailableResult()
            {
                Value = (span.TotalDays / 7).ToString(totalFormat, CultureInfo.CurrentCulture),
                Label = Resources.Microsoft_plugin_timedate_TotalWeeks,
                AlternativeSearchTag = Resources.Microsoft_plugin_timedate_SearchTagDifference,
                IconType = ResultIconType.Date,
            },
            new AvailableResult()
            {
                Value = span.TotalHours.ToString(totalFormat, CultureInfo.CurrentCulture),
                Label = Resources.Microsoft_plugin_timedate_TotalHours,
                AlternativeSearchTag = Resources.Microsoft_plugin_timedate_SearchTagDifference,
                IconType = ResultIconType.Time,
            },
            new AvailableResult()
            {
                Value = span.TotalMinutes.ToString(totalFormat, CultureInfo.CurrentCulture),
                Label = Resources.Microsoft_plugin_timedate_TotalMinutes,
                AlternativeSearchTag = Resources.Microsoft_plugin_timedate_SearchTagDifference,
                IconType = ResultIconType.Time,
            },
            new AvailableResult()
            {
                Value = span.TotalSeconds.ToString(totalFormat, CultureInfo.CurrentCulture),
                Label = Resources.Microsoft_plugin_timedate_TotalSeconds,
                AlternativeSearchTag = Resources.Microsoft_plugin_timedate_SearchTagDifference,
                IconType = ResultIconType.Time,
            },
        ];
    }

    /// <summary>
    /// Formats the distance between two dates in calendar units, e.g. "1 year, 2 months, 3 days".
    /// Whole years and months are counted from the earlier date, so month lengths and leap
    /// years are respected. The result is always positive; the totals carry the sign.
    /// </summary>
    internal static string FormatCalendarDifference(DateTime from, DateTime to)
    {
        var start = from <= to ? from : to;
        var end = from <= to ? to : from;

        var years = end.Year - start.Year;
        if (start.AddYears(years) > end)
        {
            years--;
        }

        var afterYears = start.AddYears(years);
        var months = ((end.Year - afterYears.Year) * 12) + end.Month - afterYears.Month;
        if (afterYears.AddMonths(months) > end)
        {
            months--;
        }

        var rest = end - afterYears.AddMonths(months);

        var parts = new List<string>();
        AddPart(parts, years, Resources.Microsoft_plugin_timedate_DifferenceYear, Resources.Microsoft_plugin_timedate_DifferenceYears);
        AddPart(parts, months, Resources.Microsoft_plugin_timedate_DifferenceMonth, Resources.Microsoft_plugin_timedate_DifferenceMonths);
        AddPart(parts, rest.Days, Resources.Microsoft_plugin_timedate_DifferenceDay, Resources.Microsoft_plugin_timedate_DifferenceDays);
        AddPart(parts, rest.Hours, Resources.Microsoft_plugin_timedate_DifferenceHour, Resources.Microsoft_plugin_timedate_DifferenceHours);
        AddPart(parts, rest.Minutes, Resources.Microsoft_plugin_timedate_DifferenceMinute, Resources.Microsoft_plugin_timedate_DifferenceMinutes);
        AddPart(parts, rest.Seconds, Resources.Microsoft_plugin_timedate_DifferenceSecond, Resources.Microsoft_plugin_timedate_DifferenceSeconds);

        if (parts.Count == 0)
        {
            // Same moment (or less than a second apart).
            AddPart(parts, 0, Resources.Microsoft_plugin_timedate_DifferenceDay, Resources.Microsoft_plugin_timedate_DifferenceDays, force: true);
        }

        return string.Join(Resources.Microsoft_plugin_timedate_DifferenceSeparator, parts);
    }

#pragma warning disable CA1863 // Use 'CompositeFormat'
    private static void AddPart(List<string> parts, int value, string singularFormat, string pluralFormat, bool force = false)
    {
        if (value != 0 || force)
        {
            // Because of translation we can't use 'CompositeFormat'.
            parts.Add(string.Format(CultureInfo.CurrentCulture, value == 1 ? singularFormat : pluralFormat, value));
        }
    }
#pragma warning restore CA1863 // Use 'CompositeFormat'
}
