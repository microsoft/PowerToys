// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate;

/// <summary>Maps formats already surfaced by the Time and date page to clock-format choices.</summary>
internal static class CustomClockFormatOptions
{
    internal static IEnumerable<(string Title, string Value)> Get(ISettingsInterface settings, bool includeNoText = true)
    {
        if (includeNoText)
        {
            yield return (Resources.timedate_custom_clock_format_none, string.Empty);
        }

        yield return (Resources.Microsoft_plugin_timedate_Time, "t");
        yield return (Resources.timedate_custom_clock_format_system_time_seconds, "T");
        yield return (Resources.Microsoft_plugin_timedate_Date, "d");
        yield return (Resources.Microsoft_plugin_timedate_DateAndTime, "g");
        yield return (Resources.Microsoft_plugin_timedate_TimeUtc, "UTC:t");
        yield return (Resources.Microsoft_plugin_timedate_DateAndTimeUtc, "UTC:g");
        yield return (Resources.Microsoft_plugin_timedate_Iso8601, "s");
        yield return (Resources.Microsoft_plugin_timedate_Iso8601Utc, "UTC:s");
        yield return (Resources.Microsoft_plugin_timedate_Rfc1123, "R");
        yield return (Resources.Microsoft_plugin_timedate_WeekOfYear, "WOY");
        yield return (Resources.Microsoft_plugin_timedate_Unix, "UXT");
        yield return (Resources.Microsoft_plugin_timedate_Unix_Milliseconds, "UMS");
        yield return (Resources.Microsoft_plugin_timedate_WindowsFileTime, "WFT");
        yield return (Resources.timedate_custom_clock_format_relative, "REL");

        foreach (var customFormat in settings.CustomFormats)
        {
            var parts = customFormat.Split('=', 2, System.StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
            {
                yield return (string.IsNullOrEmpty(parts[0]) ? parts[1] : parts[0], parts[1]);
            }
        }
    }
}
