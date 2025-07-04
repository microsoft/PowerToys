// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

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

    [TestCleanup]
    public void Cleanup()
    {
        // Restore original culture
        CultureInfo.CurrentCulture = originalCulture;
        CultureInfo.CurrentUICulture = originalUiCulture;
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
    [DataRow(0, DayOfWeek.Sunday)]
    [DataRow(1, DayOfWeek.Monday)]
    [DataRow(2, DayOfWeek.Tuesday)]
    [DataRow(3, DayOfWeek.Wednesday)]
    [DataRow(4, DayOfWeek.Thursday)]
    [DataRow(5, DayOfWeek.Friday)]
    [DataRow(6, DayOfWeek.Saturday)]
    [DataRow(30, null)] // wrong setting
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
    [DataRow("yyyy-MM-dd", "2023-12-25")]
    [DataRow("MM/dd/yyyy", "12/25/2023")]
    [DataRow("dd.MM.yyyy", "25.12.2023")]
    public void GetDateTimeFormatTest(string format, string expectedPattern)
    {
        // Setup
        var testDate = new DateTime(2023, 12, 25);

        // Act
        var result = testDate.ToString(format, CultureInfo.CurrentCulture);

        // Assert
        Assert.AreEqual(expectedPattern, result);
    }

    [TestMethod]
    public void GetCurrentTimeFormatTest()
    {
        // Setup
        var now = DateTime.Now;

        // Act
        var timeResult = now.ToString("T", CultureInfo.CurrentCulture);
        var dateResult = now.ToString("d", CultureInfo.CurrentCulture);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(timeResult));
        Assert.IsFalse(string.IsNullOrEmpty(dateResult));
    }

    [TestMethod]
    public void ValidateCustomDateTimeFormats()
    {
        // Setup
        var testDate = new DateTime(2023, 12, 25, 14, 30, 45);

        // Act & Assert - Test various custom formats don't crash
        try
        {
            var result1 = testDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            var result2 = testDate.ToString("dddd, MMMM dd, yyyy", CultureInfo.CurrentCulture);
            var result3 = testDate.ToString("HH:mm:ss tt", CultureInfo.CurrentCulture);

            Assert.IsFalse(string.IsNullOrEmpty(result1));
            Assert.IsFalse(string.IsNullOrEmpty(result2));
            Assert.IsFalse(string.IsNullOrEmpty(result3));
        }
        catch (Exception ex)
        {
            Assert.Fail($"Custom date format parsing failed: {ex.Message}");
        }
    }
}
