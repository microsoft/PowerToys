// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class AvailableResultsListTests
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
    public void CleanUp()
    {
        // Set culture to original value
        CultureInfo.CurrentCulture = originalCulture;
        CultureInfo.CurrentUICulture = originalUiCulture;
    }

    private DateTime GetDateTimeForTest(bool embedUtc = false)
    {
        var dateTime = new DateTime(2022, 03, 02, 22, 30, 45);
        if (embedUtc)
        {
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
        else
        {
            return dateTime;
        }
    }

    [DataTestMethod]
    [DataRow("time", "10:30 PM")]
    [DataRow("date", "3/2/2022")]
    [DataRow("date and time", "3/2/2022 10:30 PM")]
    [DataRow("hour", "22")]
    [DataRow("minute", "30")]
    [DataRow("second", "45")]
    [DataRow("millisecond", "0")]
    [DataRow("day (week day)", "Wednesday")]
    [DataRow("day of the week (week day)", "4")]
    [DataRow("day of the month", "2")]
    [DataRow("day of the year", "61")]
    [DataRow("week of the month", "1")]
    [DataRow("week of the year (calendar week, week number)", "10")]
    [DataRow("month", "March")]
    [DataRow("month of the year", "3")]
    [DataRow("month and day", "March 2")]
    [DataRow("year", "2022")]
    [DataRow("month and year", "March 2022")]
    [DataRow("ISO 8601", "2022-03-02T22:30:45")]
    [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
    [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
    [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
    public void LocalFormatsWithShortTimeAndShortDate(string formatLabel, string expectedResult)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, false, false, GetDateTimeForTest());

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value, $"Culture {CultureInfo.CurrentCulture.Name}, Culture UI: {CultureInfo.CurrentUICulture.Name}, Calendar: {CultureInfo.CurrentCulture.Calendar}, Region: {RegionInfo.CurrentRegion.Name}");
    }

    [TestMethod]
    public void GetList_WithKeywordSearch_ReturnsResults()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = AvailableResultsList.GetList(true, settings);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Should return at least some results for keyword search");
    }

    [TestMethod]
    public void GetList_WithoutKeywordSearch_ReturnsResults()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = AvailableResultsList.GetList(false, settings);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Should return at least some results for non-keyword search");
    }

    [TestMethod]
    public void GetList_WithSpecificDateTime_ReturnsFormattedResults()
    {
        // Setup
        var settings = new SettingsManager();
        var specificDateTime = GetDateTimeForTest();

        // Act
        var results = AvailableResultsList.GetList(true, settings, null, null, specificDateTime);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Should return results for specific datetime");

        // Verify that all results have values
        foreach (var result in results)
        {
            Assert.IsNotNull(result.Label, "Result label should not be null");
            Assert.IsNotNull(result.Value, "Result value should not be null");
        }
    }

    [TestMethod]
    public void GetList_ResultsHaveRequiredProperties()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = AvailableResultsList.GetList(true, settings);

        // Assert
        Assert.IsTrue(results.Count > 0, "Should have results");

        foreach (var result in results)
        {
            Assert.IsNotNull(result.Label, "Each result should have a label");
            Assert.IsNotNull(result.Value, "Each result should have a value");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Label), "Label should not be empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Value), "Value should not be empty");
        }
    }

    [TestMethod]
    public void GetList_WithDifferentCalendarSettings_ReturnsResults()
    {
        // Setup
        var settings = new SettingsManager();

        // Act & Assert - Test with different settings
        var results1 = AvailableResultsList.GetList(true, settings);
        Assert.IsNotNull(results1);
        Assert.IsTrue(results1.Count > 0);

        // Test that the method can handle different calendar settings
        var results2 = AvailableResultsList.GetList(false, settings);
        Assert.IsNotNull(results2);
        Assert.IsTrue(results2.Count > 0);
    }

    [DataTestMethod]
    [DataRow("time", "10:30 PM")]
    [DataRow("date", "Wednesday, March 2, 2022")]
    [DataRow("date and time", "Wednesday, March 2, 2022 10:30 PM")]
    [DataRow("hour", "22")]
    [DataRow("minute", "30")]
    [DataRow("second", "45")]
    [DataRow("millisecond", "0")]
    [DataRow("day (week day)", "Wednesday")]
    [DataRow("day of the week (week day)", "4")]
    [DataRow("day of the month", "2")]
    [DataRow("day of the year", "61")]
    [DataRow("week of the month", "1")]
    [DataRow("week of the year (calendar week, week number)", "10")]
    [DataRow("month", "March")]
    [DataRow("month of the year", "3")]
    [DataRow("month and day", "March 2")]
    [DataRow("year", "2022")]
    [DataRow("month and year", "March 2022")]
    [DataRow("ISO 8601", "2022-03-02T22:30:45")]
    [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
    [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
    [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
    public void LocalFormatsWithShortTimeAndLongDate(string formatLabel, string expectedResult)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, false, true, GetDateTimeForTest());

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow("time", "10:30:45 PM")]
    [DataRow("date", "3/2/2022")]
    [DataRow("date and time", "3/2/2022 10:30:45 PM")]
    [DataRow("hour", "22")]
    [DataRow("minute", "30")]
    [DataRow("second", "45")]
    [DataRow("millisecond", "0")]
    [DataRow("day (week day)", "Wednesday")]
    [DataRow("day of the week (week day)", "4")]
    [DataRow("day of the month", "2")]
    [DataRow("day of the year", "61")]
    [DataRow("week of the month", "1")]
    [DataRow("week of the year (calendar week, week number)", "10")]
    [DataRow("month", "March")]
    [DataRow("month of the year", "3")]
    [DataRow("month and day", "March 2")]
    [DataRow("year", "2022")]
    [DataRow("month and year", "March 2022")]
    [DataRow("ISO 8601", "2022-03-02T22:30:45")]
    [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
    [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
    [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
    public void LocalFormatsWithLongTimeAndShortDate(string formatLabel, string expectedResult)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, true, false, GetDateTimeForTest());

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow("time", "10:30:45 PM")]
    [DataRow("date", "Wednesday, March 2, 2022")]
    [DataRow("date and time", "Wednesday, March 2, 2022 10:30:45 PM")]
    [DataRow("hour", "22")]
    [DataRow("minute", "30")]
    [DataRow("second", "45")]
    [DataRow("millisecond", "0")]
    [DataRow("day (week day)", "Wednesday")]
    [DataRow("day of the week (week day)", "4")]
    [DataRow("day of the month", "2")]
    [DataRow("day of the year", "61")]
    [DataRow("week of the month", "1")]
    [DataRow("week of the year (calendar week, week number)", "10")]
    [DataRow("month", "March")]
    [DataRow("month of the year", "3")]
    [DataRow("month and day", "March 2")]
    [DataRow("year", "2022")]
    [DataRow("month and year", "March 2022")]
    [DataRow("ISO 8601", "2022-03-02T22:30:45")]
    [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
    [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
    [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
    public void LocalFormatsWithLongTimeAndLongDate(string formatLabel, string expectedResult)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, true, true, GetDateTimeForTest());

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow("time utc", "t")]
    [DataRow("date and time utc", "g")]
    [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
    [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
    [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
    [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
    public void UtcFormatsWithShortTimeAndShortDate(string formatLabel, string expectedFormat)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, false, false, GetDateTimeForTest(true));
        var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow("time utc", "t")]
    [DataRow("date and time utc", "f")]
    [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
    [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
    [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
    [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
    public void UtcFormatsWithShortTimeAndLongDate(string formatLabel, string expectedFormat)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, false, true, GetDateTimeForTest(true));
        var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow("time utc", "T")]
    [DataRow("date and time utc", "G")]
    [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
    [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
    [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
    [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
    public void UtcFormatsWithLongTimeAndShortDate(string formatLabel, string expectedFormat)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, true, false, GetDateTimeForTest(true));
        var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow("time utc", "T")]
    [DataRow("date and time utc", "F")]
    [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
    [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
    [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
    [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
    public void UtcFormatsWithLongTimeAndLongDate(string formatLabel, string expectedFormat)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, true, true, GetDateTimeForTest(true));
        var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [TestMethod]
    public void UnixTimestampSecondsFormat()
    {
        // Setup
        var formatLabel = "Unix epoch time";
        DateTime timeValue = DateTime.Now.ToUniversalTime();
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue);
        var expectedResult = (long)timeValue.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult.ToString(CultureInfo.CurrentCulture), result?.Value);
    }

    [TestMethod]
    public void UnixTimestampMillisecondsFormat()
    {
        // Setup
        var formatLabel = "Unix epoch time in milliseconds";
        DateTime timeValue = DateTime.Now.ToUniversalTime();
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue);
        var expectedResult = (long)timeValue.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult.ToString(CultureInfo.CurrentCulture), result?.Value);
    }

    [TestMethod]
    public void WindowsFileTimeFormat()
    {
        // Setup
        var formatLabel = "Windows file time (Int64 number)";
        DateTime timeValue = DateTime.Now;
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue);
        var expectedResult = timeValue.ToFileTime().ToString(CultureInfo.CurrentCulture);

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [TestMethod]
    public void ValidateEraResult()
    {
        // Setup
        var formatLabel = "Era";
        DateTime timeValue = DateTime.Now;
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue);
        var expectedResult = DateTimeFormatInfo.CurrentInfo.GetEraName(CultureInfo.CurrentCulture.Calendar.GetEra(timeValue));

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [TestMethod]
    public void ValidateEraAbbreviationResult()
    {
        // Setup
        var formatLabel = "Era abbreviation";
        DateTime timeValue = DateTime.Now;
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue);
        var expectedResult = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(CultureInfo.CurrentCulture.Calendar.GetEra(timeValue));

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedResult, result?.Value);
    }

    [DataTestMethod]
    [DataRow(CalendarWeekRule.FirstDay, "3")]
    [DataRow(CalendarWeekRule.FirstFourDayWeek, "2")]
    [DataRow(CalendarWeekRule.FirstFullWeek, "2")]
    public void DifferentFirstWeekSettingConfigurations(CalendarWeekRule weekRule, string expectedWeekOfYear)
    {
        // Setup
        DateTime timeValue = new DateTime(2021, 1, 12);
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue, weekRule, DayOfWeek.Sunday);

        // Act
        var resultWeekOfYear = helperResults.FirstOrDefault(x => x.Label.Equals("week of the year (calendar week, week number)", StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedWeekOfYear, resultWeekOfYear?.Value);
    }

    [DataTestMethod]
    [DataRow(DayOfWeek.Monday, "2", "2", "5")]
    [DataRow(DayOfWeek.Tuesday, "3", "3", "4")]
    [DataRow(DayOfWeek.Wednesday, "3", "3", "3")]
    [DataRow(DayOfWeek.Thursday, "3", "3", "2")]
    [DataRow(DayOfWeek.Friday, "3", "3", "1")]
    [DataRow(DayOfWeek.Saturday, "2", "2", "7")]
    [DataRow(DayOfWeek.Sunday, "2", "2", "6")]
    public void DifferentFirstDayOfWeekSettingConfigurations(DayOfWeek dayOfWeek, string expectedWeekOfYear, string expectedWeekOfMonth, string expectedDayInWeek)
    {
        // Setup
        DateTime timeValue = new DateTime(2024, 1, 12); // Friday
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings, null, null, timeValue, CalendarWeekRule.FirstDay, dayOfWeek);

        // Act
        var resultWeekOfYear = helperResults.FirstOrDefault(x => x.Label.Equals("week of the year (calendar week, week number)", StringComparison.OrdinalIgnoreCase));
        var resultWeekOfMonth = helperResults.FirstOrDefault(x => x.Label.Equals("week of the month", StringComparison.OrdinalIgnoreCase));
        var resultDayInWeek = helperResults.FirstOrDefault(x => x.Label.Equals("day of the week (week day)", StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.AreEqual(expectedWeekOfYear, resultWeekOfYear?.Value);
        Assert.AreEqual(expectedWeekOfMonth, resultWeekOfMonth?.Value);
        Assert.AreEqual(expectedDayInWeek, resultDayInWeek?.Value);
    }
}
