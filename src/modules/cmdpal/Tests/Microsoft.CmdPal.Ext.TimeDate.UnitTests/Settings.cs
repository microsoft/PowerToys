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
        bool timeWithSecond = false,
        bool dateWithWeekday = false,
        List<string>? customFormats = null)
    {
        FirstWeekOfYear = firstWeekOfYear;
        FirstDayOfWeek = firstDayOfWeek;
        TimeWithSecond = timeWithSecond;
        DateWithWeekday = dateWithWeekday;
        CustomFormats = customFormats ?? new List<string>();
    }

    // Settable so tests can change a value after construction and exercise the
    // settings-changed update paths.
    public int FirstWeekOfYear { get; set; }

    public int FirstDayOfWeek { get; set; }

    public bool TimeWithSecond { get; set; }

    public bool DateWithWeekday { get; set; }

    public List<string> CustomFormats { get; set; }
}
