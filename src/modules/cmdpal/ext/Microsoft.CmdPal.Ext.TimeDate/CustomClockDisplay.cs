// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal static class CustomClockDisplay
{
    internal static DateTimeOffset GetCurrentTime(CustomClock clock, DateTimeOffset? utcNow = null) => TimeZoneInfo.ConvertTime(
        utcNow ?? DateTimeOffset.UtcNow,
        clock.TimeZoneId == CustomClock.CurrentTimeZoneId ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId));

    internal static string GetName(CustomClock clock, DateTimeOffset? utcNow = null)
    {
        if (!string.IsNullOrWhiteSpace(clock.Title))
        {
            return clock.Title;
        }

        var timeZone = clock.TimeZoneId == CustomClock.CurrentTimeZoneId ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        var currentTime = GetCurrentTime(clock, utcNow);
        return timeZone.IsDaylightSavingTime(currentTime) && !string.IsNullOrWhiteSpace(timeZone.DaylightName)
            ? timeZone.DaylightName
            : timeZone.StandardName;
    }

    internal static string GetLocalOffsetDifference(DateTimeOffset clockTime)
    {
        var localTime = TimeZoneInfo.ConvertTime(clockTime, TimeZoneInfo.Local);
        var difference = clockTime.Offset - localTime.Offset;
        if (difference == TimeSpan.Zero)
        {
            return string.Empty;
        }

        var absoluteDifference = difference.Duration();
        var minutes = absoluteDifference.Minutes == 0 ? string.Empty : $" {absoluteDifference.Minutes}m";
        return $"{(difference < TimeSpan.Zero ? "−" : "+")}{absoluteDifference.Hours}h{minutes}";
    }

    internal static string Format(DateTimeOffset time, string format, ISettingsInterface settings)
        => new CompiledClockFormat(format).Format(time, settings);

    internal static string Format(DateTimeOffset time, CompiledClockFormat format, ISettingsInterface settings) => format.Format(time, settings);

    internal static CompiledClockFormat CompileFormat(string format) => new(format);

    internal static bool RequiresSecondUpdates(CustomClock clock) => CompileFormat(clock.TitleFormat).RequiresSecondUpdates || CompileFormat(clock.SubtitleFormat).RequiresSecondUpdates;

    internal static bool RequiresSecondUpdates(string titleFormat, string subtitleFormat) => CompileFormat(titleFormat).RequiresSecondUpdates || CompileFormat(subtitleFormat).RequiresSecondUpdates;
}
