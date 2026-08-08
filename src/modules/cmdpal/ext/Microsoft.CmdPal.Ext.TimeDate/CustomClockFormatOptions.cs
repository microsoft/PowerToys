// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate;

/// <summary>Maps formats already surfaced by the Time and date page to clock-format choices.</summary>
internal static class CustomClockFormatOptions
{
    private static readonly DateTimeOffset _exampleDateTime = new(2000, 1, 2, 15, 4, 5, TimeSpan.FromHours(2));
    private static readonly CompositeFormat _copyCommandNameFormat = CompositeFormat.Parse(Resources.timedate_copy_custom_format_command_name);

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

    internal static string GetCopyCommandName(ISettingsInterface settings, string format)
    {
        var option = Get(settings).FirstOrDefault(candidate => candidate.Value == format);
        var title = option.Title ?? WithExample(format, format, settings).Title;
        var commandTitle = IsBuiltInFormat(format) ? LowercaseFirstLetter(title) : title;
        return string.Format(CultureInfo.CurrentCulture, _copyCommandNameFormat, commandTitle);
    }

    private static bool IsBuiltInFormat(string format) => format is
        "t" or "T" or "d" or "g" or "UTC:t" or "UTC:g" or "s" or "UTC:s" or "R" or "WOY" or "UXT" or "UMS" or "WFT" or "REL";

    private static string LowercaseFirstLetter(string value)
    {
        if (string.IsNullOrEmpty(value) || (value.Length > 1 && char.IsUpper(value[1])))
        {
            return value;
        }

        return char.ToLower(value[0], CultureInfo.CurrentCulture) + value[1..];
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
