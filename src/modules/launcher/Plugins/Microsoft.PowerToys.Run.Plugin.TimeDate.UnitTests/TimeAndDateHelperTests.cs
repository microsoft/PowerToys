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
    }
}
