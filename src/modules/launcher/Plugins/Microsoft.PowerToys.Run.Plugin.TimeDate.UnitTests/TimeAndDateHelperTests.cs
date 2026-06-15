// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests
{
    [TestClass]
    public class TimeAndDateHelperTests
    {
        private CultureInfo originalCulture;
        private CultureInfo originalUiCulture;

        [TestInitialize]
        public void Setup()
        {
            // Set culture to 'en-us'
            originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-us", false);
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us", false);
        }

        [DataTestMethod]
        [DataRow(-1, null)] // default setting
        [DataRow(0, CalendarWeekRule.FirstDay)]
        [DataRow(1, CalendarWeekRule.FirstFullWeek)]
        [DataRow(2, CalendarWeekRule.FirstFourDayWeek)]
        [DataRow(30, null)] // wrong setting
        public void GetCalendarWeekRuleBasedOnPluginSetting(int setting, CalendarWeekRule? valueExpected)
        {
            // Act
            var result = TimeAndDateHelper.GetCalendarWeekRule(setting);

            // Assert
            if (valueExpected == null)
            {
                // falls back to system setting.
                Assert.AreEqual(DateTimeFormatInfo.CurrentInfo.CalendarWeekRule, result);
            }
            else
            {
                Assert.AreEqual(valueExpected, result);
            }
        }

        [DataTestMethod]
        [DataRow(-1, null)] // default setting
        [DataRow(1, DayOfWeek.Monday)]
        [DataRow(2, DayOfWeek.Tuesday)]
        [DataRow(3, DayOfWeek.Wednesday)]
        [DataRow(4, DayOfWeek.Thursday)]
        [DataRow(5, DayOfWeek.Friday)]
        [DataRow(6, DayOfWeek.Saturday)]
        [DataRow(0, DayOfWeek.Sunday)]
        [DataRow(70, null)] // wrong setting
        public void GetFirstDayOfWeekBasedOnPluginSetting(int setting, DayOfWeek? valueExpected)
        {
            // Act
            var result = TimeAndDateHelper.GetFirstDayOfWeek(setting);

            // Assert
            if (valueExpected == null)
            {
                // falls back to system setting.
                Assert.AreEqual(DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek, result);
            }
            else
            {
                Assert.AreEqual(valueExpected, result);
            }
        }

        [DataTestMethod]
        [DataRow(0, "12/30/1899 12:00 PM", 0.5)] // OLE Automation date
        [DataRow(1, "12/31/1898 12:00 PM", null)] // Excel based 1900: Date to low
        [DataRow(1, "1/1/1900, 00:00 AM", 1.0)] // Excel based 1900
        [DataRow(2, "12/31/1898 12:00 PM", null)] // Excel based 1904: Date to low
        [DataRow(2, "1/1/1904, 00:00 AM", 0.0)] // Excel based 1904
        public void ConvertToOADateFormat(int type, string date, double? valueExpected)
        {
            // Act
            DateTime dt = DateTime.Parse(date, DateTimeFormatInfo.CurrentInfo);

            // Assert
            if (valueExpected == null)
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => TimeAndDateHelper.ConvertToOleAutomationFormat(dt, (OADateFormats)type));
            }
            else
            {
                var result = TimeAndDateHelper.ConvertToOleAutomationFormat(dt, (OADateFormats)type);
                Assert.AreEqual(valueExpected, result);
            }
        }

        [DataTestMethod]
        [DataRow("dow")]
        [DataRow("\\DOW")]
        [DataRow("wom")]
        [DataRow("\\WOM")]
        [DataRow("woy")]
        [DataRow("\\WOY")]
        [DataRow("eab")]
        [DataRow("\\EAB")]
        [DataRow("wft")]
        [DataRow("\\WFT")]
        [DataRow("uxt")]
        [DataRow("\\UXT")]
        [DataRow("ums")]
        [DataRow("\\UMS")]
        [DataRow("oad")]
        [DataRow("\\OAD")]
        [DataRow("exc")]
        [DataRow("\\EXC")]
        [DataRow("exf")]
        [DataRow("\\EXF")]
        [DataRow("My super Test String with \\EXC pattern.")]
        public void CustomFormatIgnoreInvalidPattern(string format)
        {
            // Act
            string result = TimeAndDateHelper.ConvertToCustomFormat(DateTime.Now, 0, 0, 1, "AD", format, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);

            // Assert
            Assert.AreEqual(format, result);
        }

        [DataTestMethod]
        [DataRow("DOW")]
        [DataRow("DIM")]
        [DataRow("WOM")]
        [DataRow("WOY")]
        [DataRow("EAB")]
        [DataRow("WFT")]
        [DataRow("UXT")]
        [DataRow("UMS")]
        [DataRow("OAD")]
        [DataRow("EXC")]
        [DataRow("EXF")]
        public void CustomFormatReplacesValidPattern(string format)
        {
            // Act
            string result = TimeAndDateHelper.ConvertToCustomFormat(DateTime.Now, 0, 0, 1, "AD", format, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);

            // Assert
            Assert.IsFalse(result.Contains(format, StringComparison.CurrentCulture));
        }

        [DataTestMethod]
        [DataRow("01/01/0001", 1)] // First possible date
        [DataRow("12/31/9999", 5)] // Last possible date
        [DataRow("03/20/2025", 4)]
        [DataRow("09/01/2025", 1)] // First day in month is first day of week
        [DataRow("03/03/2025", 2)] // First monday is in second week
        public void GetWeekOfMonth(string date, int week)
        {
            // Act
            int result = TimeAndDateHelper.GetWeekOfMonth(DateTime.Parse(date, CultureInfo.GetCultureInfo("en-us")), DayOfWeek.Monday);

            // Assert
            Assert.AreEqual(result, week);
        }

        // Fixed reference "now" used by all friendly-format tests. Picking a midday weekday avoids
        // accidentally crossing a midnight boundary when adjusting by hours, and avoids landing on a
        // month/year boundary that would mask off-by-one mistakes elsewhere.
        private static readonly DateTime FriendlyReferenceNow = new DateTime(2026, 06, 15, 12, 00, 00);

        [DataTestMethod]
        [DataRow(0, "Today")]
        [DataRow(-1, "Yesterday")]
        [DataRow(1, "Tomorrow")]
        [DataRow(-2, "2 days ago")]
        [DataRow(-7, "7 days ago")]
        [DataRow(2, "in 2 days")]
        [DataRow(7, "in 7 days")]
        public void GetFriendlyDate_WithinSupportedWindow_ReturnsLocalizedLabel(int dayDelta, string expected)
        {
            // Arrange
            DateTime target = FriendlyReferenceNow.AddDays(dayDelta);

            // Act
            string result = TimeAndDateHelper.GetFriendlyDate(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(-8)]
        [DataRow(-30)]
        [DataRow(8)]
        [DataRow(365)]
        public void GetFriendlyDate_OutsideSupportedWindow_ReturnsNull(int dayDelta)
        {
            // Arrange
            DateTime target = FriendlyReferenceNow.AddDays(dayDelta);

            // Act
            string result = TimeAndDateHelper.GetFriendlyDate(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetFriendlyDate_TargetBeforeMidnight_ComparesByCalendarDay()
        {
            // Reference "now" at 00:30 today; target at 23:00 yesterday. 22.5h delta but one calendar day apart.
            DateTime now = new DateTime(2026, 06, 15, 00, 30, 00);
            DateTime target = new DateTime(2026, 06, 14, 23, 00, 00);

            string result = TimeAndDateHelper.GetFriendlyDate(target, now, CultureInfo.CurrentCulture);

            Assert.AreEqual("Yesterday", result);
        }

        [DataTestMethod]
        [DataRow(0, "just now")]
        [DataRow(-15, "just now")] // within 30s threshold
        [DataRow(29, "just now")]
        public void GetFriendlyDateTime_WithinJustNowThreshold_ReturnsJustNow(int secondsDelta, string expected)
        {
            DateTime target = FriendlyReferenceNow.AddSeconds(secondsDelta);

            string result = TimeAndDateHelper.GetFriendlyDateTime(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(-30, "1 minutes ago")] // boundary: 30s rounds up to 1 minute past
        [DataRow(-300, "5 minutes ago")]
        [DataRow(-3540, "59 minutes ago")]
        [DataRow(300, "in 5 minutes")]
        [DataRow(60, "in 1 minutes")]
        public void GetFriendlyDateTime_WithinHour_ReturnsMinutesLabel(int secondsDelta, string expected)
        {
            DateTime target = FriendlyReferenceNow.AddSeconds(secondsDelta);

            string result = TimeAndDateHelper.GetFriendlyDateTime(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(-1, "1 hours ago")]
        [DataRow(-3, "3 hours ago")]
        [DataRow(-23, "23 hours ago")]
        [DataRow(2, "in 2 hours")]
        [DataRow(23, "in 23 hours")]
        public void GetFriendlyDateTime_WithinDay_ReturnsHoursLabel(int hoursDelta, string expected)
        {
            DateTime target = FriendlyReferenceNow.AddHours(hoursDelta);

            string result = TimeAndDateHelper.GetFriendlyDateTime(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(-24)]
        [DataRow(-48)]
        [DataRow(24)]
        [DataRow(720)]
        public void GetFriendlyDateTime_AtOrBeyondDay_ReturnsNull(int hoursDelta)
        {
            DateTime target = FriendlyReferenceNow.AddHours(hoursDelta);

            string result = TimeAndDateHelper.GetFriendlyDateTime(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            Assert.IsNull(result);
        }

        [DataTestMethod]
        [DataRow(-3570, "1 hours ago")]      // 59m30s ago: rounds to 60 min, promotes to 1h
        [DataRow(-3599, "1 hours ago")]      // 59m59s ago
        [DataRow(3570, "in 1 hours")]
        [DataRow(3599, "in 1 hours")]
        public void GetFriendlyDateTime_MinuteBoundaryRollsToHours(int secondsDelta, string expected)
        {
            DateTime target = FriendlyReferenceNow.AddSeconds(secondsDelta);

            string result = TimeAndDateHelper.GetFriendlyDateTime(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(-23.5)]   // rounds to 24 hours, defer to friendly date
        [DataRow(-23.99)]
        [DataRow(23.5)]
        [DataRow(23.99)]
        public void GetFriendlyDateTime_HourBoundaryReturnsNull(double hoursDelta)
        {
            DateTime target = FriendlyReferenceNow.AddHours(hoursDelta);

            string result = TimeAndDateHelper.GetFriendlyDateTime(target, FriendlyReferenceNow, CultureInfo.CurrentCulture);

            Assert.IsNull(result);
        }

        [TestCleanup]
        public void CleanUp()
        {
            // Set culture to original value
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
