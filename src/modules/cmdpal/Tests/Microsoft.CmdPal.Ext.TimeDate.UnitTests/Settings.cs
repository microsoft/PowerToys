// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly int firstWeekOfYear;
    private readonly int firstDayOfWeek;
    private readonly bool enableFallbackItems;
    private readonly bool timeWithSecond;
    private readonly bool dateWithWeekday;
    private readonly int clockBandDateMode;
    private readonly string customDateFormatInClockBand;
    private readonly bool clockBandOpensNotificationCenter;
    private readonly List<string> customFormats;

    public Settings(
        int firstWeekOfYear = -1,
        int firstDayOfWeek = -1,
        bool enableFallbackItems = true,
        bool timeWithSecond = false,
        bool dateWithWeekday = false,
        int clockBandDateMode = 0,
        string customDateFormatInClockBand = "",
        bool clockBandOpensNotificationCenter = true,
        List<string> customFormats = null)
    {
        this.firstWeekOfYear = firstWeekOfYear;
        this.firstDayOfWeek = firstDayOfWeek;
        this.enableFallbackItems = enableFallbackItems;
        this.timeWithSecond = timeWithSecond;
        this.dateWithWeekday = dateWithWeekday;
        this.clockBandDateMode = clockBandDateMode;
        this.customDateFormatInClockBand = customDateFormatInClockBand;
        this.clockBandOpensNotificationCenter = clockBandOpensNotificationCenter;
        this.customFormats = customFormats ?? new List<string>();
    }

    public int FirstWeekOfYear => firstWeekOfYear;

    public int FirstDayOfWeek => firstDayOfWeek;

    public bool EnableFallbackItems => enableFallbackItems;

    public bool TimeWithSecond => timeWithSecond;

    public bool DateWithWeekday => dateWithWeekday;

    public int ClockBandDateMode => clockBandDateMode;

    public string CustomDateFormatInClockBand => customDateFormatInClockBand;

    public bool ClockBandOpensNotificationCenter => clockBandOpensNotificationCenter;

    public List<string> CustomFormats => customFormats;
}
