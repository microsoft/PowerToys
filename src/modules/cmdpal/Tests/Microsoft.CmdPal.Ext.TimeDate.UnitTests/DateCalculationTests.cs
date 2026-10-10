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
public class DateCalculationTests
{
    private static readonly DateTime Now = new(2025, 1, 31, 10, 30, 0);

    private CultureInfo originalCulture = null!;
    private CultureInfo originalUiCulture = null!;

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
    [DataRow("2025-06-27 + 3d", "2025-06-30T00:00:00")]
    [DataRow("2025-06-27+3d", "2025-06-30T00:00:00")]
    [DataRow("2025-06-27 - 3 days", "2025-06-24T00:00:00")]
    [DataRow("2025-06-27-3d", "2025-06-24T00:00:00")]
    [DataRow("2025-06-27 - 2w + 1d", "2025-06-14T00:00:00")]
    [DataRow("2025-06-27 + 1 year + 2 months", "2026-08-27T00:00:00")]
    [DataRow("2024-02-29 + 1y", "2025-02-28T00:00:00")]
    [DataRow("2025-06-27 10:00 + 90 min", "2025-06-27T11:30:00")]
    [DataRow("2025-06-27 10:00 + 2h - 30s", "2025-06-27T11:59:30")]
    [DataRow("now + 1h", "2025-01-31T11:30:00")]
    [DataRow("NOW+1H", "2025-01-31T11:30:00")]
    [DataRow("today + 1 month", "2025-02-28T00:00:00")]
    [DataRow("tomorrow + 1d", "2025-02-02T00:00:00")]
    [DataRow("yesterday - 1mo", "2024-12-30T00:00:00")]
    public void DateWithDurations(string input, string expected)
    {
        // Act
        var success = DateCalculationParser.TryEvaluate(input, Now, out var result, out var error);

        // Assert
        Assert.IsTrue(success, $"'{input}' wasn't recognized as a calculation. Error: {error}");
        Assert.IsFalse(result!.IsDifference);
        Assert.AreEqual(DateTime.Parse(expected, CultureInfo.InvariantCulture), result.Timestamp);
    }

    [TestMethod]
    public void PrefixedNumberWithDuration()
    {
        // Act
        var success = DateCalculationParser.TryEvaluate("u0 + 1d", Now, out var result, out _);

        // Assert (Unix timestamps are converted to local time)
        Assert.IsTrue(success);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(0).LocalDateTime.AddDays(1), result!.Timestamp);
    }

    [DataTestMethod]
    [DataRow("2025.06.30-2025.06.27", "2025-06-27", "2025-06-30")]
    [DataRow("2025-06-30 - 2025-06-27", "2025-06-27", "2025-06-30")]
    [DataRow("2024-01-01-2023-12-01", "2023-12-01", "2024-01-01")]
    [DataRow("2025-06-27 - 2025-06-30", "2025-06-30", "2025-06-27")]
    [DataRow("6/30/2025 - 6/27/2025", "2025-06-27", "2025-06-30")]
    [DataRow("2025-12-25 - today", "2025-01-31", "2025-12-25")]
    [DataRow("today-2025-01-01", "2025-01-01", "2025-01-31")]
    public void DifferenceBetweenDates(string input, string expectedFrom, string expectedTo)
    {
        // Act
        var success = DateCalculationParser.TryEvaluate(input, Now, out var result, out var error);

        // Assert
        Assert.IsTrue(success, $"'{input}' wasn't recognized as a calculation. Error: {error}");
        Assert.IsTrue(result!.IsDifference);
        Assert.AreEqual(DateTime.Parse(expectedFrom, CultureInfo.InvariantCulture), result.From);
        Assert.AreEqual(DateTime.Parse(expectedTo, CultureInfo.InvariantCulture), result.To);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("today")] // A date on its own isn't a calculation
    [DataRow("time")]
    [DataRow("2025-06-27 + 3")] // Durations need a unit
    [DataRow("2025-06-27 + 3 apples")]
    [DataRow("not a date + 3d")]
    [DataRow("2025-06-27 - nonsense")]
    [DataRow("+3d")]
    public void NotACalculation(string input)
    {
        // Act
        var success = DateCalculationParser.TryEvaluate(input, Now, out var result, out var error);

        // Assert
        Assert.IsFalse(success);
        Assert.IsNull(result);
        Assert.AreEqual(string.Empty, error);
    }

    [DataTestMethod]
    [DataRow("now + 99999 years")]
    [DataRow("9999-12-31 + 1d")]
    [DataRow("0001-01-01 - 1d")]
    public void OutOfRangeReportsError(string input)
    {
        // Act
        var success = DateCalculationParser.TryEvaluate(input, Now, out var result, out var error);

        // Assert
        Assert.IsFalse(success);
        Assert.IsNull(result);
        Assert.AreEqual(Resources.Microsoft_plugin_timedate_InvalidInput_CalculationOutOfRange, error);
    }

    [DataTestMethod]
    [DataRow("2025-06-27", "2025-06-30", "3 days")]
    [DataRow("2025-06-30", "2025-06-27", "3 days")]
    [DataRow("2025-06-27", "2025-06-28", "1 day")]
    [DataRow("2025-06-27", "2025-06-27", "0 days")]
    [DataRow("2024-01-31", "2024-03-01", "1 month, 1 day")]
    [DataRow("2020-02-29", "2021-02-28", "1 year")]
    [DataRow("2023-12-01", "2025-02-03", "1 year, 2 months, 2 days")]
    [DataRow("2025-01-01 00:00:00", "2025-01-02 03:04:05", "1 day, 3 hours, 4 minutes, 5 seconds")]
    [DataRow("2025-01-01 00:00:00", "2025-01-01 01:01:01", "1 hour, 1 minute, 1 second")]
    public void CalendarDifference(string from, string to, string expected)
    {
        // Act
        var result = AvailableResultsList.FormatCalendarDifference(
            DateTime.Parse(from, CultureInfo.InvariantCulture),
            DateTime.Parse(to, CultureInfo.InvariantCulture));

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void DifferenceResults()
    {
        // Act
        var results = AvailableResultsList.GetDifferenceList(new DateTime(2025, 6, 27), new DateTime(2025, 7, 11, 12, 0, 0));

        // Assert
        Assert.AreEqual("14 days, 12 hours", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_Difference).Value);
        Assert.AreEqual("14.5", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_TotalDays).Value);
        Assert.AreEqual("2.07", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_TotalWeeks).Value);
        Assert.AreEqual("348", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_TotalHours).Value);
        Assert.AreEqual("20,880", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_TotalMinutes).Value);
        Assert.AreEqual("1,252,800", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_TotalSeconds).Value);
    }

    [TestMethod]
    public void NegativeDifferenceKeepsSignOnTotals()
    {
        // Act
        var results = AvailableResultsList.GetDifferenceList(new DateTime(2025, 6, 30), new DateTime(2025, 6, 27));

        // Assert
        Assert.AreEqual("3 days", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_Difference).Value);
        Assert.AreEqual("-3", results.Single(r => r.Label == Resources.Microsoft_plugin_timedate_TotalDays).Value);
    }

    [TestMethod]
    public void SearchShowsDifferenceResults()
    {
        // Act
        var results = TimeDateCalculator.ExecuteSearch(new Settings(), "2025.06.30-2025.06.27");

        // Assert
        Assert.AreEqual(6, results.Count);
        Assert.IsTrue(results.Any(r => r.Title == "3 days" && r.Subtitle == Resources.Microsoft_plugin_timedate_Difference));
        Assert.IsTrue(results.Any(r => r.Title == "3" && r.Subtitle == Resources.Microsoft_plugin_timedate_TotalDays));
    }

    [TestMethod]
    public void SearchCanFilterDifferenceResults()
    {
        // Act
        var results = TimeDateCalculator.ExecuteSearch(new Settings(), "total hours::2025-06-30 - 2025-06-27");

        // Assert
        Assert.AreEqual("72", results.First().Title);
        Assert.AreEqual(Resources.Microsoft_plugin_timedate_TotalHours, results.First().Subtitle);
    }

    [TestMethod]
    public void SearchShowsFormatsForCalculatedDate()
    {
        // Act
        var results = TimeDateCalculator.ExecuteSearch(new Settings(), "2025-06-27 + 3d");

        // Assert
        Assert.IsTrue(results.Count > 1);
        Assert.IsTrue(results.Any(r => r.Title == "6/30/2025" && r.Subtitle == Resources.Microsoft_plugin_timedate_Date));
    }

    [TestMethod]
    public void SearchUsesCurrentTimeForKeywords()
    {
        // Arrange
        var currentTime = new DateTimeOffset(Now, TimeSpan.Zero);

        // Act
        var results = TimeDateCalculator.ExecuteSearch(new Settings(), "date::today + 1d", currentTime);

        // Assert
        Assert.AreEqual("2/1/2025", results.First().Title);
    }

    [TestMethod]
    public void SearchReportsOutOfRangeCalculation()
    {
        // Act
        var results = TimeDateCalculator.ExecuteSearch(new Settings(), "9999-12-31 + 1d");

        // Assert
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(Resources.Microsoft_plugin_timedate_InvalidInput_CalculationOutOfRange, results[0].Details?.Body);
    }

    [TestMethod]
    public void UtcOffsetInputKeepsItsMeaning()
    {
        // "+1" without a unit is still read as a UTC offset by the plain date parser.
        var results = TimeDateCalculator.ExecuteSearch(new Settings(), "2025-06-27+1");
        var expected = new DateTimeOffset(2025, 6, 27, 0, 0, 0, TimeSpan.FromHours(1)).LocalDateTime;

        Assert.IsTrue(results.Any(r => r.Title == expected.ToString("s", CultureInfo.InvariantCulture)));
    }
}
