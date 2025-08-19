// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
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

    [DataTestMethod]
    [DataRow("time", 1)] // Common time queries should return results
    [DataRow("date", 1)] // Common date queries should return results
    [DataRow("now", 1)] // Now should return multiple results
    [DataRow("current", 1)] // Current should return multiple results
    [DataRow("year", 1)] // Year-related queries should return results
    [DataRow("time::10:10:10", 1)] // Specific time format should return results
    [DataRow("date::10/10/10", 1)] // Specific date format should return results
    public void CountBasicQueries(string query, int expectedMinResultCount)
    {
        // Setup
        var settings = new Settings();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsTrue(
            results.Count >= expectedMinResultCount,
            $"Expected at least {expectedMinResultCount} results for query '{query}', but got {results.Count}");
    }

    [DataTestMethod]
    [DataRow("time", "time")]
    [DataRow("date", "date")]
    [DataRow("year", "year")]
    [DataRow("now", "now")]
    [DataRow("year", "year")]
    public void BasicQueryTest(string input, string expectedMatchTerm)
    {
        var settings = new Settings();
        var page = new TimeDateExtensionPage(settings);
        page.UpdateSearchText(string.Empty, input);
        var resultLists = page.GetItems();

        var result = Query(input, resultLists);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0, "No items matched the query.");

        var firstItem = result.FirstOrDefault();
        Assert.IsNotNull(firstItem, "No items matched the query.");
        Assert.IsTrue(
            firstItem.Title.Contains(expectedMatchTerm, System.StringComparison.OrdinalIgnoreCase) ||
            firstItem.Subtitle.Contains(expectedMatchTerm, System.StringComparison.OrdinalIgnoreCase),
            $"Expected to match '{expectedMatchTerm}' in title or subtitle but got '{firstItem.Title}' - '{firstItem.Subtitle}'");
    }

    [DataTestMethod]
    [DataRow("unix", "Unix epoch time")]
    [DataRow("unix epoch time in milli", "Unix epoch time in milliseconds")]
    [DataRow("file", "Windows file time (Int64 number)")]
    [DataRow("hour", "Hour")]
    [DataRow("minute", "Minute")]
    [DataRow("second", "Second")]
    [DataRow("millisecond", "Millisecond")]
    [DataRow("day", "Day (Week day)")]
    [DataRow("day of week", "Day of the week (Week day)")]
    [DataRow("day of month", "Day of the month")]
    [DataRow("day of year", "Day of the year")]
    [DataRow("week of month", "Week of the month")]
    [DataRow("week of year", "Week of the year (Calendar week, Week number)")]
    [DataRow("month", "Month")]
    [DataRow("month of year", "Month of the year")]
    [DataRow("month and d", "Month and day")]
    [DataRow("year", "Year")]
    [DataRow("universal", "Universal time format: YYYY-MM-DD hh:mm:ss")]
    [DataRow("rfc", "RFC1123")]
    [DataRow("time::12:30", "Time")]
    [DataRow("date::10.10.2022", "Date")]
    [DataRow("time::u1646408119", "Time")]
    [DataRow("time::ft637820085517321977", "Time")]
    [DataRow("week day", "Day (Week day)")]
    [DataRow("cal week", "Week of the year (Calendar week, Week number)")]
    [DataRow("week num", "Week of the year (Calendar week, Week number)")]
    [DataRow("days in mo", "Days in month")]
    [DataRow("Leap y", "Leap year")]
    public void FormatDateQueryTest(string input, string expectedMatchTerm)
    {
        var settings = new Settings();
        var page = new TimeDateExtensionPage(settings);
        page.UpdateSearchText(string.Empty, input);
        var resultLists = page.GetItems();

        var firstItem = resultLists.FirstOrDefault();
        Assert.IsNotNull(firstItem, "No items matched the query.");
        Assert.IsTrue(
            firstItem.Title.Contains(expectedMatchTerm, System.StringComparison.OrdinalIgnoreCase) ||
            firstItem.Subtitle.Contains(expectedMatchTerm, System.StringComparison.OrdinalIgnoreCase),
            $"Expected to match '{expectedMatchTerm}' in title or subtitle but got '{firstItem.Title}' - '{firstItem.Subtitle}'");
    }

    [DataTestMethod]
    [DataRow("abcdefg")]
    [DataRow("timmmmeeee")]
    [DataRow("timtaaaetetaae::u1646408119")]
    [DataRow("time:eeee")]
    [DataRow("time::eeee")]
    [DataRow("time//eeee")]
    [DataRow("ug1646408119")] // Invalid prefix
    [DataRow("u9999999999999")] // Unix number + prefix is longer than 12 characters
    [DataRow("ums999999999999999")] // Unix number in milliseconds + prefix is longer than 17 characters
    [DataRow("-u99999999999")] // Unix number with wrong placement of - sign
    [DataRow("+ums9999999999")] // Unix number in milliseconds with wrong placement of + sign
    [DataRow("0123456")] // Missing prefix
    [DataRow("ft63782008ab55173dasdas21977")] // Number contains letters
    [DataRow("ft63782008ab55173dasdas")] // Number contains letters at the end
    [DataRow("ft12..548")] // Number contains wrong punctuation
    [DataRow("ft12..54//8")] // Number contains wrong punctuation and other characters
    [DataRow("time::ft12..54//8")] // Number contains wrong punctuation and other characters
    [DataRow("ut2ed.5555")] // Number contains letters
    [DataRow("12..54//8")] // Number contains punctuation and other characters, but no special prefix
    [DataRow("ft::1288gg8888")] // Number contains delimiter and letters, but no special prefix
    [DataRow("date::12::55")]
    [DataRow("date::12:aa:55")]
    [DataRow("10.aa.22")]
    [DataRow("12::55")]
    [DataRow("12:aa:55")]
    public void InvalidInputShowsErrorResults(string query)
    {
        var settings = new Settings();
        var page = new TimeDateExtensionPage(settings);
        page.UpdateSearchText(string.Empty, query);
        var results = page.GetItems();

        // Assert
        Assert.IsNotNull(results, $"Results should not be null for query '{query}'");
        Assert.IsTrue(results.Length > 0, $"Query '{query}' should return at least one result");

        var firstItem = results.FirstOrDefault();
        Assert.IsTrue(firstItem.Title.StartsWith("Error: Invalid input", StringComparison.CurrentCulture), $"Query '{query}' should return an error result for invalid input");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void EmptyQueryReturnsAllResults(string input)
    {
        var settings = new Settings();
        var page = new TimeDateExtensionPage(settings);
        page.UpdateSearchText("abc", input);
        var results = page.GetItems();

        // Assert
        Assert.IsTrue(results.Length > 0, $"Empty query should return results");
    }

    [DataTestMethod]
    [DataRow("time u", "Time UTC")]
    [DataRow("now u", "Now UTC")]
    [DataRow("iso utc", "ISO 8601 UTC")]
    [DataRow("iso zone", "ISO 8601 with time zone")]
    [DataRow("iso utc zone", "ISO 8601 UTC with time zone")]
    public void TimeZoneQuery(string query, string expectedSubtitle)
    {
        var settings = new Settings();
        var page = new TimeDateExtensionPage(settings);
        page.UpdateSearchText(string.Empty, query);
        var resultsList = page.GetItems();
        var results = Query(query, resultsList);

        // Assert
        Assert.IsNotNull(results);
        var firstResult = results.FirstOrDefault();
        Assert.IsTrue(firstResult.Subtitle.StartsWith(expectedSubtitle, StringComparison.CurrentCulture), $"Could not find result with subtitle starting with '{expectedSubtitle}' for query '{query}'");
    }

    [DataTestMethod]
    [DataRow("time::12:30:45", "12:30 PM")]
    [DataRow("date::2023-12-25", "12/25/2023")]
    [DataRow("now::u1646408119", "132908817190000000")]
    public void DelimiterQueriesReturnResults(string query, string expectedResult)
    {
        var settings = new Settings();
        var page = new TimeDateExtensionPage(settings);
        page.UpdateSearchText(string.Empty, query);
        var resultsList = page.GetItems();

        // Assert
        Assert.IsNotNull(resultsList);
        var firstResult = resultsList.FirstOrDefault();
        Assert.IsTrue(firstResult.Title.Contains(expectedResult, StringComparison.CurrentCulture), $"Delimiter query '{query}' result not match {expectedResult} current result {firstResult.Title}");
    }
}
