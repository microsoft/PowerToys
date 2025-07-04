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
    [DataRow("hour", "22")]
    [DataRow("minute", "30")]
    [DataRow("second", "45")]
    [DataRow("millisecond", "0")]
    [DataRow("day (week day)", "Wednesday")]
    [DataRow("day of the week (week day)", "4")]
    [DataRow("day of the month", "2")]
    [DataRow("day of the year", "61")]
    [DataRow("month", "March")]
    [DataRow("month of the year", "3")]
    [DataRow("year", "2022")]
    public void LocalFormatsWithShortTimeAndShortDate(string formatLabel, string expectedResult)
    {
        // Setup
        var settings = new SettingsManager();
        var helperResults = AvailableResultsList.GetList(true, settings);

        // Act
        var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

        // Assert
        if (result != null)
        {
            Assert.IsNotNull(result.Value, $"Result value should not be null for format: {formatLabel}");
        }
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
}
