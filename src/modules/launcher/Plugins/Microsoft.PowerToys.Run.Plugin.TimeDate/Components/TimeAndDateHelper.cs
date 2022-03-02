// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    internal static class TimeAndDateHelper
    {
        /// <summary>
        /// Get the format for the time string
        /// </summary>
        /// <param name="targetFormat">Type of format</param>
        /// <param name="timeLong">Show date with weekday and name of month (long format)</param>
        /// <param name="dateLong">Show time with seconds (long format)</param>
        /// <returns>String that identifies the time/date format (<see href="https://docs.microsoft.com/en-us/dotnet/api/system.datetime.tostring"/>)</returns>
        internal static string GetStringFormat(TimestampType targetFormat, bool timeLong, bool dateLong)
        {
            switch (targetFormat)
            {
                case TimestampType.Time:
                    return timeLong ? "T" : "t";
                case TimestampType.Date:
                    return dateLong ? "D" : "d";
                case TimestampType.DateTime:
                    if (timeLong & dateLong)
                    {
                        return "F"; // Friday, October 31, 2008 5:04:32 PM
                    }
                    else if (timeLong & !dateLong)
                    {
                        return "G"; // 10/31/2008 5:04:32 PM
                    }
                    else if (!timeLong & dateLong)
                    {
                        return "f"; // Friday, October 31, 2008 5:04 PM
                    }
                    else
                    {
                        // (!timeLong & !dateLong)
                        return "g"; // 10/31/2008 5:04 PM
                    }

                default:
                    return string.Empty; // Windows default based on current culture settings
            }
        }

        /// <summary>
        /// Returns the number week in the month (Used code from 'David Morton' from <see href="https://social.msdn.microsoft.com/Forums/vstudio/en-US/bf504bba-85cb-492d-a8f7-4ccabdf882cb/get-week-number-for-month"/>)
        /// </summary>
        /// <param name="date">date</param>
        /// <returns>Number of week in the month</returns>
        internal static int GetWeekOfMonth(DateTime date)
        {
            DateTime beginningOfMonth = new DateTime(date.Year, date.Month, 1);

            while (date.Date.AddDays(1).DayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
            {
                date = date.AddDays(1);
            }

            return (int)Math.Truncate((double)date.Subtract(beginningOfMonth).TotalDays / 7f) + 1;
        }
    }

    /// <summary>
    /// Type of time/date format
    /// </summary>
    internal enum TimestampType
    {
        Time,
        Date,
        DateTime,
    }
}
