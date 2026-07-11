// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

internal static class TimeAndDateHelper
{
    /* htcfreek:Currently not used.
     * private static readonly Regex _regexSpecialInputFormats = new Regex(@"^.*(u|ums|ft|oa|exc|exf)\d"); */

    private static readonly Regex _regexCustomDateTimeFormats = new Regex(@"(?<!\\)(DOW|DIM|WOM|WOY|IWOY|IWYR|IWYY|IDOW|EAB|WFT|UXT|UMS|OAD|EXC|EXF)");
    private static readonly Regex _regexCustomDateTimeDim = new Regex(@"(?<!\\)DIM");
    private static readonly Regex _regexCustomDateTimeDow = new Regex(@"(?<!\\)DOW");
    private static readonly Regex _regexCustomDateTimeWom = new Regex(@"(?<!\\)WOM");
    private static readonly Regex _regexCustomDateTimeWoy = new Regex(@"(?<!\\)WOY");
    private static readonly Regex _regexCustomDateTimeIwoy = new Regex(@"(?<!\\)IWOY");
    private static readonly Regex _regexCustomDateTimeIwyr = new Regex(@"(?<!\\)IWYR");
    private static readonly Regex _regexCustomDateTimeIwyy = new Regex(@"(?<!\\)IWYY");
    private static readonly Regex _regexCustomDateTimeIdow = new Regex(@"(?<!\\)IDOW");
    private static readonly Regex _regexCustomDateTimeEab = new Regex(@"(?<!\\)EAB");
    private static readonly Regex _regexCustomDateTimeWft = new Regex(@"(?<!\\)WFT");
    private static readonly Regex _regexCustomDateTimeUxt = new Regex(@"(?<!\\)UXT");
    private static readonly Regex _regexCustomDateTimeUms = new Regex(@"(?<!\\)UMS");
    private static readonly Regex _regexCustomDateTimeOad = new Regex(@"(?<!\\)OAD");
    private static readonly Regex _regexCustomDateTimeExc = new Regex(@"(?<!\\)EXC");
    private static readonly Regex _regexCustomDateTimeExf = new Regex(@"(?<!\\)EXF");

    private static readonly CompositeFormat _dockWeekNumberFormat = CompositeFormat.Parse(Resources.timedate_dock_week_number_format);
    private static readonly CompositeFormat _dockDateWeekFormat = CompositeFormat.Parse(Resources.timedate_dock_date_week_format);

    private const long UnixTimeSecondsMin = -62135596800;
    private const long UnixTimeSecondsMax = 253402300799;
    private const long UnixTimeMillisecondsMin = -62135596800000;
    private const long UnixTimeMillisecondsMax = 253402300799999;
    private const long WindowsFileTimeMin = 0;
    private const long WindowsFileTimeMax = 2650467707991000000;
    private const double OADateMin = -657434.99999999;
    private const double OADateMax = 2958465.99999999;
    private const double Excel1900DateMin = 1;
    private const double Excel1900DateMax = 2958465.99998843;
    private const double Excel1904DateMin = 0;
    private const double Excel1904DateMax = 2957003.99998843;

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
        var daysInWeek = 7;
        var adjustment = 1; // We count from 1 to 7 and not from 0 to 6

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
    /// Convert input string to a <see cref="DateTime"/> object in local time
    /// </summary>
    /// <param name="input">String with date/time</param>
    /// <param name="timestamp">The new <see cref="DateTime"/> object</param>
    /// <param name="inputParsingErrorMsg">Error message shown to the user</param>
    /// <returns>True on success; otherwise, false</returns>
    internal static bool ParseStringAsDateTime(in string input, out DateTime timestamp, out string inputParsingErrorMsg)
    {
        inputParsingErrorMsg = string.Empty;
        CompositeFormat errorMessage = CompositeFormat.Parse(Resources.Microsoft_plugin_timedate_InvalidInput_SupportedRange);

        if (DateTime.TryParse(input, out timestamp))
        {
            // Known date/time format
            Logger.LogDebug($"Successfully parsed standard date/time format: '{input}' as {timestamp}");
            return true;
        }
        else if (Regex.IsMatch(input, @"^u[\+-]?\d+$"))
        {
            // Unix time stamp
            // We use long instead of int, because int is too small after 03:14:07 UTC 2038-01-19
            var canParse = long.TryParse(input.TrimStart('u'), out var secondsU);

            // Value has to be in the range from -62135596800 to 253402300799
            if (!canParse || secondsU < UnixTimeSecondsMin || secondsU > UnixTimeSecondsMax)
            {
                inputParsingErrorMsg = string.Format(CultureInfo.CurrentCulture, errorMessage, Resources.Microsoft_plugin_timedate_Unix, UnixTimeSecondsMin, UnixTimeSecondsMax);
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse unix timestamp: '{input}'. Value out of range.");
                return false;
            }

            timestamp = DateTimeOffset.FromUnixTimeSeconds(secondsU).LocalDateTime;
            Logger.LogDebug($"Successfully parsed unix timestamp: '{input}' as {timestamp}");
            return true;
        }
        else if (Regex.IsMatch(input, @"^ums[\+-]?\d+$"))
        {
            // Unix time stamp in milliseconds
            // We use long instead of int because int is too small after 03:14:07 UTC 2038-01-19
            var canParse = long.TryParse(input.TrimStart("ums".ToCharArray()), out var millisecondsUms);

            // Value has to be in the range from -62135596800000 to 253402300799999
            if (!canParse || millisecondsUms < UnixTimeMillisecondsMin || millisecondsUms > UnixTimeMillisecondsMax)
            {
                inputParsingErrorMsg = string.Format(CultureInfo.CurrentCulture, errorMessage, Resources.Microsoft_plugin_timedate_Unix_Milliseconds, UnixTimeMillisecondsMin, UnixTimeMillisecondsMax);
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse unix millisecond timestamp: '{input}'. Value out of range.");
                return false;
            }

            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(millisecondsUms).LocalDateTime;
            Logger.LogDebug($"Successfully parsed unix millisecond timestamp: '{input}' as {timestamp}");
            return true;
        }
        else if (Regex.IsMatch(input, @"^ft\d+$"))
        {
            var canParse = long.TryParse(input.TrimStart("ft".ToCharArray()), out var secondsFt);

            // Windows file time
            // Value has to be in the range from 0 to 2650467707991000000
            if (!canParse || secondsFt < WindowsFileTimeMin || secondsFt > WindowsFileTimeMax)
            {
                inputParsingErrorMsg = string.Format(CultureInfo.CurrentCulture, errorMessage, Resources.Microsoft_plugin_timedate_WindowsFileTime, WindowsFileTimeMin, WindowsFileTimeMax);
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse Windows file time: '{input}'. Value out of range.");
                return false;
            }

            // DateTime.FromFileTime returns as local time.
            timestamp = DateTime.FromFileTime(secondsFt);
            Logger.LogDebug($"Successfully parsed Windows file time: '{input}' as {timestamp}");
            return true;
        }
        else if (Regex.IsMatch(input, @"^oa[+-]?\d+[,.0-9]*$"))
        {
            var canParse = double.TryParse(input.TrimStart("oa".ToCharArray()), out var oADate);

            // OLE Automation date
            // Input has to be in the range from -657434.99999999 to 2958465.99999999
            // DateTime.FromOADate returns as local time.
            if (!canParse || oADate < OADateMin || oADate > OADateMax)
            {
                inputParsingErrorMsg = string.Format(CultureInfo.CurrentCulture, errorMessage, Resources.Microsoft_plugin_timedate_OADate, OADateMin, OADateMax);
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse OLE Automation date: '{input}'. Value out of range.");
                return false;
            }

            timestamp = DateTime.FromOADate(oADate);
            Logger.LogDebug($"Successfully parsed OLE Automation date: '{input}' as {timestamp}");
            return true;
        }
        else if (Regex.IsMatch(input, @"^exc[+-]?\d+[,.0-9]*$"))
        {
            var canParse = double.TryParse(input.TrimStart("exc".ToCharArray()), out var excDate);

            // Excel's 1900 date value
            // Input has to be in the range from 1 (0 = Fake date) to 2958465.99998843 and not 60 whole number
            // Because of a bug in Excel and the way it behaves before 3/1/1900 we have to adjust all inputs lower than 61 for +1
            // DateTime.FromOADate returns as local time.
            if (!canParse || excDate < 0 || excDate > Excel1900DateMax)
            {
                // For the if itself we use 0 as min value that we can show a special message if input is 0.
                inputParsingErrorMsg = string.Format(CultureInfo.CurrentCulture, errorMessage, Resources.Microsoft_plugin_timedate_Excel1900, Excel1900DateMin, Excel1900DateMax);
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse Excel 1900 date value: '{input}'. Value out of range.");
                return false;
            }

            if (Math.Truncate(excDate) == 0 || Math.Truncate(excDate) == 60)
            {
                inputParsingErrorMsg = Resources.Microsoft_plugin_timedate_InvalidInput_FakeExcel1900;
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse Excel 1900 date value: '{input}'. Invalid date (0 or 60).");
                return false;
            }

            excDate = excDate <= 60 ? excDate + 1 : excDate;
            timestamp = DateTime.FromOADate(excDate);
            Logger.LogDebug($"Successfully parsed Excel 1900 date value: '{input}' as {timestamp}");
            return true;
        }
        else if (Regex.IsMatch(input, @"^exf[+-]?\d+[,.0-9]*$"))
        {
            var canParse = double.TryParse(input.TrimStart("exf".ToCharArray()), out var exfDate);

            // Excel's 1904 date value
            // Input has to be in the range from 0 to 2957003.99998843
            // Because Excel uses 01/01/1904 as base we need to adjust for +1462
            // DateTime.FromOADate returns as local time.
            if (!canParse || exfDate < Excel1904DateMin || exfDate > Excel1904DateMax)
            {
                inputParsingErrorMsg = string.Format(CultureInfo.CurrentCulture, errorMessage, Resources.Microsoft_plugin_timedate_Excel1904, Excel1904DateMin, Excel1904DateMax);
                timestamp = new DateTime(1, 1, 1, 1, 1, 1);
                Logger.LogError($"Failed to parse Excel 1904 date value: '{input}'. Value out of range.");
                return false;
            }

            timestamp = DateTime.FromOADate(exfDate + 1462);
            Logger.LogDebug($"Successfully parsed Excel 1904 date value: '{input}' as {timestamp}");
            return true;
        }
        else
        {
            inputParsingErrorMsg = Resources.Microsoft_plugin_timedate_InvalidInput_ErrorMessageTitle;
            timestamp = new DateTime(1, 1, 1, 1, 1, 1);
            Logger.LogWarning($"Failed to parse input: '{input}'. Format not recognized.");
            return false;
        }
    }

    /* htcfreek:Currently not required
    /// <summary>
    /// Test if input is special parsing for Unix time, Unix time in milliseconds, file time, ...
    /// </summary>
    /// <param name="input">String with date/time</param>
    /// <returns>True if yes; otherwise, false</returns>
    internal static bool IsSpecialInputParsing(string input)
    {
        return _regexSpecialInputFormats.IsMatch(input);
    }*/

    /// <summary>
    /// Converts a DateTime object based on the format string
    /// </summary>
    /// <param name="date">Date/time object.</param>
    /// <param name="unix">Value for replacing "Unix Time Stamp".</param>
    /// <param name="unixMilliseconds">Value for replacing "Unix Time Stamp in milliseconds".</param>
    /// <param name="calWeek">Value for replacing calendar week.</param>
    /// <param name="eraShortFormat">Era abbreviation.</param>
    /// <param name="format">Format definition.</param>
    /// <returns>Formatted date/time string.</returns>
    internal static string ConvertToCustomFormat(DateTime date, long unix, long unixMilliseconds, int calWeek, string eraShortFormat, string format, CalendarWeekRule firstWeekRule, DayOfWeek firstDayOfTheWeek)
    {
        var result = format;

        // The ISO 8601 tokens always calculate with ISOWeek, independent of the
        // configured first week/first day settings, so a string built from them is
        // ISO compliant by construction. They must be replaced before their
        // non-ISO counterparts (WOY, DOW) match inside them.

        // IWOY: ISO 8601 week of year, always two digits
        result = _regexCustomDateTimeIwoy.Replace(result, ISOWeek.GetWeekOfYear(date).ToString("D2", CultureInfo.CurrentCulture));

        // IWYR: ISO 8601 week-based year (differs from the calendar year at the boundary)
        result = _regexCustomDateTimeIwyr.Replace(result, ISOWeek.GetYear(date).ToString("D4", CultureInfo.CurrentCulture));

        // IWYY: ISO 8601 week-based year, two digits
        result = _regexCustomDateTimeIwyy.Replace(result, (ISOWeek.GetYear(date) % 100).ToString("D2", CultureInfo.CurrentCulture));

        // IDOW: ISO 8601 day of the week (1 = Monday .. 7 = Sunday)
        result = _regexCustomDateTimeIdow.Replace(result, (date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek).ToString(CultureInfo.CurrentCulture));

        // DOW: Number of day in week
        result = _regexCustomDateTimeDow.Replace(result, GetNumberOfDayInWeek(date, firstDayOfTheWeek).ToString(CultureInfo.CurrentCulture));

        // DIM: Days in Month
        result = _regexCustomDateTimeDim.Replace(result, DateTime.DaysInMonth(date.Year, date.Month).ToString(CultureInfo.CurrentCulture));

        // WOM: Week of Month
        result = _regexCustomDateTimeWom.Replace(result, GetWeekOfMonth(date, firstDayOfTheWeek).ToString(CultureInfo.CurrentCulture));

        // WOY: Week of Year
        result = _regexCustomDateTimeWoy.Replace(result, calWeek.ToString(CultureInfo.CurrentCulture));

        // EAB: Era abbreviation
        result = _regexCustomDateTimeEab.Replace(result, eraShortFormat);

        // WFT: Week of Month
        if (_regexCustomDateTimeWft.IsMatch(result))
        {
            // Special handling as very early dates can't convert.
            result = _regexCustomDateTimeWft.Replace(result, date.ToFileTime().ToString(CultureInfo.CurrentCulture));
        }

        // UXT: Unix time stamp
        result = _regexCustomDateTimeUxt.Replace(result, unix.ToString(CultureInfo.CurrentCulture));

        // UMS: Unix time stamp milli seconds
        result = _regexCustomDateTimeUms.Replace(result, unixMilliseconds.ToString(CultureInfo.CurrentCulture));

        // OAD: OLE Automation date
        result = _regexCustomDateTimeOad.Replace(result, ConvertToOleAutomationFormat(date, OADateFormats.OLEAutomation).ToString(CultureInfo.CurrentCulture));

        // EXC: Excel date value with base 1900
        if (_regexCustomDateTimeExc.IsMatch(result))
        {
            // Special handling as very early dates can't convert.
            result = _regexCustomDateTimeExc.Replace(result, ConvertToOleAutomationFormat(date, OADateFormats.Excel1900).ToString(CultureInfo.CurrentCulture));
        }

        // EXF: Excel date value with base 1904
        if (_regexCustomDateTimeExf.IsMatch(result))
        {
            // Special handling as very early dates can't convert.
            result = _regexCustomDateTimeExf.Replace(result, ConvertToOleAutomationFormat(date, OADateFormats.Excel1904).ToString(CultureInfo.CurrentCulture));
        }

        return result;
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
    /// Returns the week of the year based on the plugin's 'First week of the year' and
    /// 'First day of the week' settings.
    /// </summary>
    /// <param name="date">Date to get the week number for.</param>
    /// <param name="settings">Plugin settings.</param>
    /// <returns>The week number.</returns>
    internal static int GetWeekOfYear(DateTime date, ISettingsInterface settings)
    {
        return GetWeekOfYear(date, GetCalendarWeekRule(settings.FirstWeekOfYear), GetFirstDayOfWeek(settings.FirstDayOfWeek));
    }

    /// <summary>
    /// Returns the ISO 8601 week date representation (e.g. "2026-W28-1"). Always
    /// calculated with ISOWeek, so the string is ISO compliant independent of the
    /// first week/first day settings; the week-based year differs from the calendar
    /// year at year boundaries.
    /// </summary>
    /// <param name="date">Date to format.</param>
    /// <returns>The ISO 8601 week date string.</returns>
    internal static string GetIsoWeekDateString(DateTime date)
    {
        var isoDayOfWeek = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        return string.Format(CultureInfo.InvariantCulture, "{0:D4}-W{1:D2}-{2}", ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date), isoDayOfWeek);
    }

    /// <summary>
    /// Builds the date line (subtitle) of the Clock dock band for the given date and settings.
    /// </summary>
    /// <param name="dateTimeNow">Date/time to render (local time).</param>
    /// <param name="settings">Plugin settings.</param>
    /// <returns>The rendered date line.</returns>
    internal static string GetClockBandDateLine(DateTime dateTimeNow, ISettingsInterface settings)
    {
        var dateString = dateTimeNow.ToString(GetStringFormat(FormatStringType.Date, false, false), CultureInfo.CurrentCulture);

        switch (settings.ClockBandDateMode)
        {
            case 1: // Date and week number (e.g. "06.07.2026 · W28")
                var weekString = string.Format(CultureInfo.CurrentCulture, _dockWeekNumberFormat, GetWeekOfYear(dateTimeNow, settings));
                return string.Format(CultureInfo.CurrentCulture, _dockDateWeekFormat, dateString, weekString);
            case 2: // ISO 8601 week date (e.g. "2026-W28-1"), always ISO compliant
                return GetIsoWeekDateString(dateTimeNow);
            case 3: // Custom format; falls back to the system date if the format is empty or invalid
                // WOY uses the first week/first day settings here, matching the
                // behavior of the 'Custom formats' search results. The IWOY, IWYR,
                // IWYY and IDOW tokens are always ISO 8601.
                var firstWeekRule = GetCalendarWeekRule(settings.FirstWeekOfYear);
                var firstDayOfTheWeek = GetFirstDayOfWeek(settings.FirstDayOfWeek);
                var weekOfYear = GetWeekOfYear(dateTimeNow, firstWeekRule, firstDayOfTheWeek);
                return TryFormatCustomString(dateTimeNow, settings.CustomDateFormatInClockBand, weekOfYear, firstWeekRule, firstDayOfTheWeek, out var customString)
                    ? customString
                    : dateString;
            default: // System date only
                return dateString;
        }
    }

    /// <summary>
    /// Formats a single date/time using our custom format syntax (same syntax as the
    /// 'Custom formats' setting). Used for the Clock dock band.
    /// </summary>
    /// <param name="dateTimeNow">Date/time to format (local time).</param>
    /// <param name="formatSyntax">Format definition, optionally prefixed with "UTC:".</param>
    /// <param name="weekOfYear">Week number to use for the WOY placeholder.</param>
    /// <param name="firstWeekRule">Rule for the first week of the year.</param>
    /// <param name="firstDayOfTheWeek">First day of the week.</param>
    /// <param name="result">The formatted string on success.</param>
    /// <returns>True on success; otherwise, false (e.g. empty or invalid format).</returns>
    internal static bool TryFormatCustomString(DateTime dateTimeNow, string formatSyntax, int weekOfYear, CalendarWeekRule firstWeekRule, DayOfWeek firstDayOfTheWeek, out string result)
    {
        result = string.Empty;

        if (string.IsNullOrWhiteSpace(formatSyntax))
        {
            return false;
        }

        try
        {
            var calendar = CultureInfo.CurrentCulture.Calendar;
            var dateTimeNowUtc = dateTimeNow.ToUniversalTime();
            var dtObject = dateTimeNow;
            var syntax = formatSyntax.Trim();

            if (syntax.StartsWith("UTC:", StringComparison.InvariantCulture))
            {
                dtObject = dateTimeNowUtc;
                syntax = syntax.Substring(4);
            }

            var unixTimestamp = ((DateTimeOffset)dateTimeNowUtc).ToUnixTimeSeconds();
            var unixTimestampMilliseconds = ((DateTimeOffset)dateTimeNowUtc).ToUnixTimeMilliseconds();
            var eraShort = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(calendar.GetEra(dateTimeNow));
            var value = ConvertToCustomFormat(dtObject, unixTimestamp, unixTimestampMilliseconds, weekOfYear, eraShort, syntax, firstWeekRule, firstDayOfTheWeek);
            try
            {
                value = dtObject.ToString(value, CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                // Unlike a transient search result, the dock shows this string permanently,
                // so an invalid format falls back to the plain system date instead of
                // showing the raw pattern text.
                Logger.LogWarning($"Invalid custom Clock band format '{formatSyntax}': {ex.Message}");
                return false;
            }

            result = value;
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to format the custom Clock band format '{formatSyntax}': {ex.Message}");
            return false;
        }
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
/// Different versions of Date formats based on OLE Automation date
/// </summary>
internal enum OADateFormats
{
    OLEAutomation,
    Excel1900,
    Excel1904,
}
