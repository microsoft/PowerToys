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
    private readonly List<string> customFormats;

    public Settings(
        int firstWeekOfYear = -1,
        int firstDayOfWeek = -1,
        bool enableFallbackItems = true,
        bool timeWithSecond = false,
        bool dateWithWeekday = false,
        List<string> customFormats = null)
    {
        this.firstWeekOfYear = firstWeekOfYear;
        this.firstDayOfWeek = firstDayOfWeek;
        this.enableFallbackItems = enableFallbackItems;
        this.timeWithSecond = timeWithSecond;
        this.dateWithWeekday = dateWithWeekday;
        this.customFormats = customFormats ?? new List<string>();
    }

    public int FirstWeekOfYear => firstWeekOfYear;

    public int FirstDayOfWeek => firstDayOfWeek;

    public bool EnableFallbackItems => enableFallbackItems;

    public bool TimeWithSecond => timeWithSecond;

    public bool DateWithWeekday => dateWithWeekday;

    public List<string> CustomFormats => customFormats;
}
