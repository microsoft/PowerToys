// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    /// <summary>
    /// Tests for the shared week of year calculation (search results and Clock dock
    /// band) and the ISO 8601 custom format tokens, with a focus on the year boundary
    /// (week 52/53 vs. week 1) on different weekdays.
    /// </summary>
    [TestClass]
    public class WeekOfYearTests
    {
        [DataTestMethod]

        // Regression for the known Calendar.GetWeekOfYear defect: it returns 53 for
        // 2012-12-31 (a Monday) although ISO 8601 says week 1 of 2013.
        [DataRow(2012, 12, 31, 1)]
        [DataRow(2012, 12, 30, 52)] // Sunday, last day of week 52/2012

        // Long ISO years (53 weeks): 2015 and 2020
        [DataRow(2015, 12, 28, 53)] // Monday, first day of week 53/2015
        [DataRow(2016, 1, 1, 53)] // Friday, still week 53/2015
        [DataRow(2016, 1, 4, 1)] // Monday, first day of week 1/2016
        [DataRow(2020, 12, 31, 53)] // Thursday
        [DataRow(2021, 1, 1, 53)] // Friday, still week 53/2020
        [DataRow(2021, 1, 3, 53)] // Sunday, last day of week 53/2020
        [DataRow(2021, 1, 4, 1)] // Monday, first day of week 1/2021

        // Week 1 reaching back into the old year: 2024/2025
        [DataRow(2024, 12, 29, 52)] // Sunday, last day of week 52/2024
        [DataRow(2024, 12, 30, 1)] // Monday, already week 1/2025
        [DataRow(2024, 12, 31, 1)] // Tuesday
        [DataRow(2025, 1, 1, 1)] // Wednesday

        // 2026 is a long ISO year (Jan 1st is a Thursday)
        [DataRow(2025, 12, 29, 1)] // Monday, already week 1/2026
        [DataRow(2026, 1, 1, 1)] // Thursday
        [DataRow(2026, 12, 31, 53)] // Thursday
        [DataRow(2027, 1, 3, 53)] // Sunday, still week 53/2026
        [DataRow(2027, 1, 4, 1)] // Monday, first day of week 1/2027
        public void IsoSettingsMatchIso8601AtYearBoundaries(int year, int month, int day, int expectedWeek)
        {
            var week = TimeAndDateHelper.GetWeekOfYear(new DateTime(year, month, day), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            Assert.AreEqual(expectedWeek, week);
        }

        [TestMethod]
        public void SettingsOverloadUsesTheFirstWeekAndFirstDaySettings()
        {
            // First four-day week + Monday = ISO 8601, so the boundary is corrected
            var isoSettings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1);
            Assert.AreEqual(1, TimeAndDateHelper.GetWeekOfYear(new DateTime(2012, 12, 31), isoSettings));

            // US style settings (week 1 contains January 1st, weeks start on Sunday)
            var usSettings = new Settings(firstWeekOfYear: 0, firstDayOfWeek: 0);
            Assert.AreEqual(1, TimeAndDateHelper.GetWeekOfYear(new DateTime(2021, 1, 1), usSettings));
        }

        [DataTestMethod]

        // US system = Excel WEEKNUM option 1: the week containing January 1st is
        // always week 1, weeks start on Sunday.
        [DataRow(2021, 1, 1, 1)] // ISO says 53 here; US says 1
        [DataRow(2016, 1, 1, 1)] // ISO says 53 here; US says 1
        [DataRow(2024, 12, 30, 53)]
        [DataRow(2026, 12, 31, 53)]
        [DataRow(2026, 1, 4, 2)] // Sunday starts week 2; ISO says week 1
        public void UsSettingsMatchExcelWeeknumOption1(int year, int month, int day, int expectedWeek)
        {
            var week = TimeAndDateHelper.GetWeekOfYear(new DateTime(year, month, day), CalendarWeekRule.FirstDay, DayOfWeek.Sunday);

            Assert.AreEqual(expectedWeek, week);
        }

        [DataTestMethod]

        // Excel WEEKNUM option 2: the week containing January 1st is week 1, weeks
        // start on Monday.
        [DataRow(2026, 1, 4, 1)] // Sunday, still week 1
        [DataRow(2026, 1, 5, 2)] // Monday starts week 2
        public void MondayFirstDaySettingsMatchExcelWeeknumOption2(int year, int month, int day, int expectedWeek)
        {
            var week = TimeAndDateHelper.GetWeekOfYear(new DateTime(year, month, day), CalendarWeekRule.FirstDay, DayOfWeek.Monday);

            Assert.AreEqual(expectedWeek, week);
        }

        [TestMethod]
        public void NonIsoCombinationsDelegateToTheCalendar()
        {
            // First four-day week with a non-Monday start is a valid non-ISO
            // calculation and must keep the plain Calendar behavior.
            var date = new DateTime(2012, 12, 31);
            var expected = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);

            Assert.AreEqual(expected, TimeAndDateHelper.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday));
        }

        [DataTestMethod]

        // The IWOY/IWYR/IDOW tokens always render the ISO 8601 week date, including
        // the week-based year at the boundary and the zero-padded week number.
        [DataRow(2026, 7, 6, "2026-W28-1")] // Monday
        [DataRow(2027, 1, 1, "2026-W53-5")] // Friday, week-based year is still 2026
        [DataRow(2024, 12, 30, "2025-W01-1")] // Monday, week-based year is already 2025
        [DataRow(2026, 1, 4, "2026-W01-7")] // Sunday is day 7 in ISO 8601
        [DataRow(2026, 1, 20, "2026-W04-2")] // Weeks 1-9 are zero-padded ("W04", not "W4")
        public void IsoTokensRenderTheIsoWeekDate(int year, int month, int day, string expected)
        {
            var date = new DateTime(year, month, day);
            var weekOfYear = TimeAndDateHelper.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var success = TimeAndDateHelper.TryFormatCustomString(date, "IWYR-\\WIWOY-IDOW", weekOfYear, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday, out var result);

            Assert.IsTrue(success);
            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]

        // IWYY renders the two-digit ISO week-based year, correctly paired with the
        // ISO week number at the boundary.
        [DataRow(2025, 12, 29, "26 W01")] // Monday, week-based year is already 2026
        [DataRow(2026, 7, 6, "26 W28")]
        [DataRow(2027, 1, 1, "26 W53")] // Friday, week-based year is still 2026
        public void IwyyTokenRendersTheTwoDigitIsoWeekBasedYear(int year, int month, int day, string expected)
        {
            var date = new DateTime(year, month, day);
            var weekOfYear = TimeAndDateHelper.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var success = TimeAndDateHelper.TryFormatCustomString(date, "IWYY \\WIWOY", weekOfYear, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday, out var result);

            Assert.IsTrue(success);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SystemDefaultSettingsResolveToIsoOnAnIsoLocale()
        {
            // de-DE resolves to first four-day week + Monday, so "System default"
            // (-1) must go through the ISOWeek calculation at the boundary.
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE", false);
                var settings = new Settings();

                Assert.AreEqual(1, TimeAndDateHelper.GetWeekOfYear(new DateTime(2012, 12, 31), settings));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public void IsoWeekDateModeIsIsoCompliantIndependentOfTheSettings()
        {
            // US style calculation settings; the ISO week date entry must still
            // render the ISO compliant string.
            var settings = new Settings(firstWeekOfYear: 0, firstDayOfWeek: 0, clockBandDateMode: 2);

            var dateLine = TimeAndDateHelper.GetClockBandDateLine(new DateTime(2027, 1, 1), settings);

            Assert.AreEqual("2026-W53-5", dateLine);
        }

        [TestMethod]
        public void IsoTokensIgnoreTheFirstWeekAndFirstDaySettings()
        {
            // 2012-12-31 with US style settings: WOY renders the configured week
            // number while IWOY stays ISO 8601 (week 01 of 2013).
            var date = new DateTime(2012, 12, 31);
            var weekOfYear = TimeAndDateHelper.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);

            var success = TimeAndDateHelper.TryFormatCustomString(date, "WOY IWOY", weekOfYear, CalendarWeekRule.FirstDay, DayOfWeek.Sunday, out var result);

            Assert.IsTrue(success);
            Assert.AreEqual($"{weekOfYear.ToString(CultureInfo.CurrentCulture)} 01", result);
        }

        [TestMethod]
        public void DateLineUsesTheIsoWeekNumberAtTheYearBoundary()
        {
            // ISO settings; Calendar.GetWeekOfYear would render 53 here
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 1);

            var dateLine = TimeAndDateHelper.GetClockBandDateLine(new DateTime(2012, 12, 31), settings);

            Assert.IsTrue(dateLine.EndsWith("W1", StringComparison.Ordinal), $"Expected the date line to end with 'W1' but got '{dateLine}'");
            Assert.IsFalse(dateLine.Contains("53", StringComparison.Ordinal), $"Expected no week 53 in '{dateLine}'");
        }

        [TestMethod]
        public void CustomFormatWoyPlaceholderFollowsTheFirstWeekAndFirstDaySettings()
        {
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 3, customDateFormatInClockBand: "\\WWOY");

            var dateLine = TimeAndDateHelper.GetClockBandDateLine(new DateTime(2026, 7, 6), settings);

            Assert.AreEqual("W28", dateLine);
        }
    }
}
