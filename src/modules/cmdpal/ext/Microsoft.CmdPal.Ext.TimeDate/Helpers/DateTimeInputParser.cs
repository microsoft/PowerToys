// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

/// <summary>
/// Parses user input into a <see cref="DateTime"/>, covering both the standard culture
/// formats and the numeric timestamp formats (Unix time, Unix time in milliseconds,
/// Windows file time, OLE Automation and Excel serial dates).
/// </summary>
internal static class DateTimeInputParser
{
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
}
