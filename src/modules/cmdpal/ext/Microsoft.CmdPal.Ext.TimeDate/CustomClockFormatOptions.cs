// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate;

/// <summary>Maps formats already surfaced by the Time and date page to clock-format choices.</summary>
internal static class CustomClockFormatOptions
{
    private static readonly DateTimeOffset _exampleDateTime = new(2000, 1, 2, 15, 4, 5, TimeSpan.FromHours(2));

    internal static IEnumerable<(string Title, string Value)> Get(ISettingsInterface settings, bool includeNoText = true)
    {
        if (includeNoText)
        {
            yield return (Resources.timedate_custom_clock_format_none, string.Empty);
        }

        yield return WithExample(Resources.Microsoft_plugin_timedate_Time, "t", settings);
        yield return WithExample(Resources.timedate_custom_clock_format_system_time_seconds, "T", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_Date, "d", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_DateAndTime, "g", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_TimeUtc, "UTC:t", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_DateAndTimeUtc, "UTC:g", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_Iso8601, "s", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_Iso8601Utc, "UTC:s", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_Rfc1123, "R", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_WeekOfYear, "WOY", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_Unix, "UXT", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_Unix_Milliseconds, "UMS", settings);
        yield return WithExample(Resources.Microsoft_plugin_timedate_WindowsFileTime, "WFT", settings);
        yield return WithExample(Resources.timedate_custom_clock_format_relative, "REL", settings);

        foreach (var customFormat in settings.CustomFormats)
        {
            var parts = customFormat.Split('=', 2, System.StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
            {
                yield return WithExample(string.IsNullOrEmpty(parts[0]) ? parts[1] : parts[0], parts[1], settings);
            }
        }
    }

    private static (string Title, string Value) WithExample(string title, string format, ISettingsInterface settings)
    {
        try
        {
            var example = CustomClockDisplay.Format(_exampleDateTime, format, settings);
            return string.IsNullOrEmpty(example) ? (title, format) : ($"{title} ({example})", format);
        }
        catch (FormatException)
        {
            return (title, format);
        }
    }
}
