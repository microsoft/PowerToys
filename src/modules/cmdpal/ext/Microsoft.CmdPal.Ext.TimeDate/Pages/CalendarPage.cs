// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

internal sealed partial class CalendarPage : ContentPage
{
    public CalendarPage()
    {
        Id = "com.microsoft.cmdpal.timedate.calendarPage";
        Name = Resources.timedate_calendar_page_name;
        Icon = Icons.TimeDateExtIcon;
    }

    public override IContent[] GetContent()
    {
        var now = DateTime.Now;
        Title = now.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        return [new MarkdownContent(GenerateCalendarMarkdown(now))];
    }

    private static string GenerateCalendarMarkdown(DateTime now)
    {
        var sb = new StringBuilder();
        var culture = CultureInfo.CurrentCulture;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
        var abbreviatedDayNames = culture.DateTimeFormat.AbbreviatedDayNames;

        // Header row: abbreviated day names starting from the culture's first day of week
        sb.Append('|');
        for (var i = 0; i < 7; i++)
        {
            var dayIndex = ((int)firstDayOfWeek + i) % 7;
            sb.Append($" {abbreviatedDayNames[dayIndex]} |");
        }

        sb.AppendLine();

        // Separator
        sb.Append('|');
        for (var i = 0; i < 7; i++)
        {
            sb.Append(":-:|");
        }

        sb.AppendLine();

        // Calculate how many blank cells appear before day 1
        var firstDayDow = (int)firstDayOfMonth.DayOfWeek;
        var offset = ((firstDayDow - (int)firstDayOfWeek) + 7) % 7;

        sb.Append('|');
        for (var i = 0; i < offset; i++)
        {
            sb.Append("   |");
        }

        var col = offset;
        for (var day = 1; day <= daysInMonth; day++)
        {
            if (day == now.Day)
            {
                sb.Append($" **{day}** |");
            }
            else
            {
                sb.Append($" {day} |");
            }

            col++;

            if (col == 7 && day < daysInMonth)
            {
                sb.AppendLine();
                sb.Append('|');
                col = 0;
            }
        }

        // Pad the last row with empty cells
        while (col > 0 && col < 7)
        {
            sb.Append("   |");
            col++;
        }

        sb.AppendLine();

        return sb.ToString();
    }
}
