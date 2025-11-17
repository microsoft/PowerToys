// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class AvailableResultsList
    {
        /// <summary>
        /// Returns a list with all available date time formats
        /// </summary>
        /// <param name="isKeywordSearch">Is this a search with plugin activation keyword or not</param>
        /// <param name="timeLongFormat">Required for UnitTest: Show time in long format</param>
        /// <param name="dateLongFormat">Required for UnitTest: Show date in long format</param>
        /// <param name="timestamp">Use custom <see cref="DateTime"/> object to calculate results instead of the system date/time</param>
        /// <param name="firstWeekOfYear">Required for UnitTest: Use custom first week of the year instead of the plugin setting.</param>
        /// <param name="firstDayOfWeek">Required for UnitTest: Use custom first day of the week instead the plugin setting.</param>
        /// <returns>List of results</returns>
        internal static List<AvailableResult> GetList(bool isKeywordSearch, bool? timeLongFormat = null, bool? dateLongFormat = null, DateTime? timestamp = null, CalendarWeekRule? firstWeekOfYear = null, DayOfWeek? firstDayOfWeek = null)
        {
            List<AvailableResult> results = new List<AvailableResult>();
            Calendar calendar = CultureInfo.CurrentCulture.Calendar;

            bool timeExtended = timeLongFormat ?? TimeDateSettings.Instance.TimeWithSeconds;
            bool dateExtended = dateLongFormat ?? TimeDateSettings.Instance.DateWithWeekday;
            bool isSystemDateTime = timestamp == null;
            DateTime dateTimeNow = timestamp ?? DateTime.Now;
            DateTime dateTimeNowUtc = dateTimeNow.ToUniversalTime();
            CalendarWeekRule firstWeekRule = firstWeekOfYear ?? TimeAndDateHelper.GetCalendarWeekRule(TimeDateSettings.Instance.CalendarFirstWeekRule);
            DayOfWeek firstDayOfTheWeek = firstDayOfWeek ?? TimeAndDateHelper.GetFirstDayOfWeek(TimeDateSettings.Instance.FirstDayOfWeek);

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
                int weekOfYear = calendar.GetWeekOfYear(dateTimeNow, firstWeekRule, firstDayOfTheWeek);
                string era = DateTimeFormatInfo.CurrentInfo.GetEraName(calendar.GetEra(dateTimeNow));
                string eraShort = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(calendar.GetEra(dateTimeNow));

                // Custom formats
                foreach (string f in TimeDateSettings.Instance.CustomFormats)
                {
                    string[] formatParts = f.Split("=", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string formatSyntax = formatParts.Length == 2 ? formatParts[1] : string.Empty;
                    string searchTags = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagCustom");
                    DateTime dtObject = dateTimeNow;

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

                            bool containsCustomSyntax = TimeAndDateHelper.StringContainsCustomFormatSyntax(formatSyntax);
                            if (formatSyntax.StartsWith("UTC:", StringComparison.InvariantCulture))
                            {
                                searchTags = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagCustomUtc");
                                dtObject = dateTimeNowUtc;
                            }

                            // Get formated date
                            var value = TimeAndDateHelper.ConvertToCustomFormat(dtObject, unixTimestamp, unixTimestampMilliseconds, weekOfYear, eraShort, Regex.Replace(formatSyntax, "^UTC:", string.Empty), firstWeekRule, firstDayOfTheWeek);
                            try
                            {
                                value = dtObject.ToString(value, CultureInfo.CurrentCulture);
                            }
                            catch
                            {
                                if (!containsCustomSyntax)
                                {
                                    throw;
                                }
                                else
                                {
                                    // Do not fail as we have custom format syntax. Instead fix backslashes.
                                    value = Regex.Replace(value, @"(?<!\\)\\", string.Empty).Replace("\\\\", "\\");
                                }
                            }

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
                            Wox.Plugin.Logger.Log.Exception($"Failed to convert into custom format {formatParts[0]}: {formatSyntax}", e, typeof(AvailableResultsList));
                            results.Add(new AvailableResult()
                            {
                                Value = Resources.Microsoft_plugin_timedate_ErrorConvertCustomFormat + " " + e.Message,
                                Label = formatParts[0],
                                AlternativeSearchTag = searchTags,
                                IconType = ResultIconType.Error,
                            });
                        }
                        catch (Exception e)
                        {
                            Wox.Plugin.Logger.Log.Exception($"Failed to convert into custom format {formatParts[0]}: {formatSyntax}", e, typeof(AvailableResultsList));
                            results.Add(new AvailableResult()
                            {
                                Value = Resources.Microsoft_plugin_timedate_InvalidCustomFormat + " " + formatSyntax,
                                Label = formatParts[0],
                                AlternativeSearchTag = searchTags,
                                IconType = ResultIconType.Error,
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
                        Value = dateTimeNow.ToFileTime().ToString(CultureInfo.CurrentCulture),
                        Label = Resources.Microsoft_plugin_timedate_WindowsFileTime,
                        AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
                        IconType = ResultIconType.DateTime,
                    });
                }
                catch
                {
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
