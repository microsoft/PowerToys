// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

public class Settings : ISettingsInterface
{
    public Settings(
        int firstWeekOfYear = -1,
        int firstDayOfWeek = -1,
        bool enableFallbackItems = true,
        bool timeWithSecond = false,
        bool dockClockWithSecond = false,
        bool dateWithWeekday = false,
        int clockBandDateMode = 0,
        string customDateFormatInClockBand = "",
        bool clockBandOpensNotificationCenter = true,
        List<string>? customFormats = null)
    {
        FirstWeekOfYear = firstWeekOfYear;
        FirstDayOfWeek = firstDayOfWeek;
        EnableFallbackItems = enableFallbackItems;
        TimeWithSecond = timeWithSecond;
        DockClockWithSecond = dockClockWithSecond;
        DateWithWeekday = dateWithWeekday;
        ClockBandDateMode = clockBandDateMode;
        CustomDateFormatInClockBand = customDateFormatInClockBand;
        ClockBandOpensNotificationCenter = clockBandOpensNotificationCenter;
        CustomFormats = customFormats ?? new List<string>();
    }

    // Settable so tests can change a value after construction and exercise the
    // settings-changed update paths.
    public int FirstWeekOfYear { get; set; }

    public int FirstDayOfWeek { get; set; }

    public bool EnableFallbackItems { get; set; }

    public bool TimeWithSecond { get; set; }

    public bool DockClockWithSecond { get; set; }

    public bool DateWithWeekday { get; set; }

    public int ClockBandDateMode { get; set; }

    public string CustomDateFormatInClockBand { get; set; }

    public bool ClockBandOpensNotificationCenter { get; set; }

    public List<string> CustomFormats { get; set; }
}
