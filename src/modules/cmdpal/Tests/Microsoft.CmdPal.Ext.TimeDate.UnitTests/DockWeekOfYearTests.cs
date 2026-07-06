// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    /// <summary>
    /// Week numbering tests for the Clock dock band, with a focus on the ISO 8601
    /// year boundary (week 52/53 vs. week 1) on different weekdays.
    /// </summary>
    [TestClass]
    public class DockWeekOfYearTests
    {
        private static Settings IsoSettings() => new(clockBandDateMode: 1);

        private static Settings UsSettings() => new(clockBandDateMode: 2);

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
        public void IsoSystemMatchesIso8601AtYearBoundaries(int year, int month, int day, int expectedWeek)
        {
            var week = TimeAndDateHelper.GetDockWeekOfYear(new DateTime(year, month, day), IsoSettings());

            Assert.AreEqual(expectedWeek, week);
        }

        [DataTestMethod]

        // US system: the week containing January 1st is always week 1 (Excel WEEKNUM option 1)
        [DataRow(2021, 1, 1, 1)] // ISO says 53 here; US says 1
        [DataRow(2016, 1, 1, 1)] // ISO says 53 here; US says 1
        [DataRow(2024, 12, 30, 53)] // ISO says 1 here; US says 53
        [DataRow(2026, 12, 31, 53)]
        [DataRow(2026, 1, 4, 2)] // First Sunday of 2026 starts week 2
        public void UsSystemMatchesExcelWeeknumOption1(int year, int month, int day, int expectedWeek)
        {
            var week = TimeAndDateHelper.GetDockWeekOfYear(new DateTime(year, month, day), UsSettings());

            Assert.AreEqual(expectedWeek, week);
        }

        [DataTestMethod]

        // Custom system with first day Monday and 'week 1 contains January 1st'
        // (Excel WEEKNUM option 2/11): January 1st is always week 1.
        [DataRow(2021, 1, 1, 1)]
        [DataRow(2016, 1, 1, 1)]
        public void CustomSystemMatchesExcelWeeknumOption2(int year, int month, int day, int expectedWeek)
        {
            var settings = new Settings(firstWeekOfYear: 0, firstDayOfWeek: 1, clockBandDateMode: 3);

            var week = TimeAndDateHelper.GetDockWeekOfYear(new DateTime(year, month, day), settings);

            Assert.AreEqual(expectedWeek, week);
        }

        [TestMethod]
        public void IsoModeIgnoresTheCustomFirstDayAndFirstWeekSettings()
        {
            // ISO mode must ignore the custom first day/first week settings entirely.
            var isoWithConflictingCustomSettings = new Settings(firstWeekOfYear: 0, firstDayOfWeek: 0, clockBandDateMode: 1);

            var week = TimeAndDateHelper.GetDockWeekOfYear(new DateTime(2012, 12, 31), isoWithConflictingCustomSettings);

            Assert.AreEqual(1, week);
        }

        [DataTestMethod]

        // ISO week date incl. the week-based year, which differs from the calendar
        // year at the boundary (2027-01-01 belongs to 2026-W53).
        [DataRow(2026, 7, 6, "2026-W28-1")] // Monday
        [DataRow(2027, 1, 1, "2026-W53-5")] // Friday, ISO year is still 2026
        [DataRow(2024, 12, 30, "2025-W01-1")] // Monday, ISO year is already 2025
        [DataRow(2026, 1, 4, "2026-W01-7")] // Sunday is ISO day 7; week is zero-padded
        public void IsoWeekDateStringIsStandardCompliant(int year, int month, int day, string expected)
        {
            Assert.AreEqual(expected, TimeAndDateHelper.GetIsoWeekDateString(new DateTime(year, month, day)));
        }

        [TestMethod]
        public void DateLineUsesIsoWeekNumberAtYearBoundary()
        {
            // 2012-12-31 in mode 'date and ISO week number' must show W1, not W53.
            var settings = new Settings(clockBandDateMode: 1);

            var line = TimeAndDateHelper.GetClockBandDateLine(new DateTime(2012, 12, 31), settings);

            StringAssert.EndsWith(line, "W1");
            Assert.IsFalse(line.Contains("53"), $"Date line '{line}' must not contain the wrong week number 53.");
        }

        [TestMethod]
        public void DateLineIsoWeekDateModeUsesWeekBasedYear()
        {
            var settings = new Settings(clockBandDateMode: 4);

            var line = TimeAndDateHelper.GetClockBandDateLine(new DateTime(2027, 1, 1), settings);

            Assert.AreEqual("2026-W53-5", line);
        }

        [TestMethod]
        public void CustomFormatWoyPlaceholderFollowsTheFirstWeekAndFirstDaySettings()
        {
            // WOY in the custom format uses the first week/first day settings, matching
            // the behavior of the 'Custom formats' search results.
            var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 5, customDateFormatInClockBand: "\\WWOY");

            var line = TimeAndDateHelper.GetClockBandDateLine(new DateTime(2026, 7, 6), settings);

            Assert.AreEqual("W28", line);
        }
    }
}
