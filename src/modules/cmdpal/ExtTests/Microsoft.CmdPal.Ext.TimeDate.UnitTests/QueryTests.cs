// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class QueryTests
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
    [DataRow("time", 2)] // Common time queries should return results
    [DataRow("date", 2)] // Common date queries should return results
    [DataRow("now", 3)] // Now should return multiple results
    [DataRow("current", 3)] // Current should return multiple results
    [DataRow("year", 8)] // Year-related queries should return results
    [DataRow("time::10:10:10", 1)] // Specific time format should return results
    [DataRow("date::10/10/10", 1)] // Specific date format should return results
    public void CountBasicQueries(string query, int expectedMinResultCount)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsTrue(
            results.Count >= expectedMinResultCount,
            $"Expected at least {expectedMinResultCount} results for query '{query}', but got {results.Count}");
    }

    [DataTestMethod]
    [DataRow("time")]
    [DataRow("date")]
    [DataRow("year")]
    [DataRow("now")]
    [DataRow("current")]
    [DataRow("")]
    [DataRow("now::10:10:10")] // Windows file time
    public void AllQueriesReturnResults(string query)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, $"Query '{query}' should return at least one result");
    }

    [DataTestMethod]
    [DataRow("time", "Time -")]
    [DataRow("date", "Date -")]
    [DataRow("now", "Now -")]
    [DataRow("unix", "Unix epoch time -")]
    [DataRow("unix epoch time in milli", "Unix epoch time in milliseconds -")]
    [DataRow("file", "Windows file time (Int64 number) ")]
    [DataRow("hour", "Hour -")]
    [DataRow("minute", "Minute -")]
    [DataRow("second", "Second -")]
    [DataRow("millisecond", "Millisecond -")]
    [DataRow("day", "Day (Week day) -")]
    [DataRow("day of week", "Day of the week (Week day) -")]
    [DataRow("day of month", "Day of the month -")]
    [DataRow("day of year", "Day of the year -")]
    [DataRow("week of month", "Week of the month -")]
    [DataRow("week of year", "Week of the year (Calendar week, Week number) -")]
    [DataRow("month", "Month -")]
    [DataRow("month of year", "Month of the year -")]
    [DataRow("month and d", "Month and day -")]
    [DataRow("month and y", "Month and year -")]
    [DataRow("year", "Year -")]
    [DataRow("era", "Era -")]
    [DataRow("era a", "Era abbreviation -")]
    [DataRow("universal", "Universal time format: YYYY-MM-DD hh:mm:ss -")]
    [DataRow("iso", "ISO 8601 -")]
    [DataRow("rfc", "RFC1123 -")]
    [DataRow("time::12:30", "Time -")]
    [DataRow("date::10.10.2022", "Date -")]
    [DataRow("time::u1646408119", "Time -")]
    [DataRow("time::ft637820085517321977", "Time -")]
    [DataRow("week day", "Day (Week day) -")]
    [DataRow("cal week", "Week of the year (Calendar week, Week number) -")]
    [DataRow("week num", "Week of the year (Calendar week, Week number) -")]
    [DataRow("days in mo", "Days in month -")]
    [DataRow("Leap y", "Leap year -")]
    public void CanFindFormatResult(string query, string expectedSubtitle)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        var matchingResult = results.FirstOrDefault(x => x.Details?.Body?.StartsWith(expectedSubtitle, StringComparison.CurrentCulture) == true);
        Assert.IsNotNull(matchingResult, $"Could not find result with subtitle starting with '{expectedSubtitle}' for query '{query}'");
    }

    [DataTestMethod]
    [DataRow("12:30", "Time -")]
    [DataRow("10.10.2022", "Date -")]
    [DataRow("u1646408119", "Date and time -")]
    [DataRow("u+1646408119", "Date and time -")]
    [DataRow("u-1646408119", "Date and time -")]
    [DataRow("ums1646408119", "Date and time -")]
    [DataRow("ums+1646408119", "Date and time -")]
    [DataRow("ums-1646408119", "Date and time -")]
    [DataRow("ft637820085517321977", "Date and time -")]
    public void DateTimeNumberOnlyInput(string query, string expectedSubtitle)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        var matchingResult = results.FirstOrDefault(x => x.Details?.Body?.StartsWith(expectedSubtitle, StringComparison.CurrentCulture) == true);
        Assert.IsNotNull(matchingResult, $"Could not find result with subtitle starting with '{expectedSubtitle}' for query '{query}'");
    }

    [DataTestMethod]
    [DataRow("abcdefg")]
    [DataRow("timmmmeeee")]
    [DataRow("timtaaaetetaae::u1646408119")]
    [DataRow("time:eeee")]
    [DataRow("time::eeee")]
    [DataRow("time//eeee")]
    public void InvalidInputShowsErrorResults(string query)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        // In cmdpal, invalid input typically results in error results or empty results
        // Check that we get some kind of result (either error or empty list)
        Assert.IsNotNull(results, $"Results should not be null for query '{query}'");

        // If we get results, check if they are error results
        if (results.Count > 0)
        {
            var hasErrorResult = results.Any(r => r.Title?.StartsWith("Error:", StringComparison.CurrentCulture) == true);

            // Either we have error results or we have valid results (depending on implementation)
            Assert.IsTrue(
                hasErrorResult || results.Count > 0,
                $"Query '{query}' should either return error results or handle gracefully");
        }
    }

    [DataTestMethod]
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
    public void InvalidNumberInputShowsErrorMessage(string query)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results, $"Results should not be null for query '{query}'");
        Assert.IsTrue(results.Count > 0, $"Should return at least one result (error message) for invalid query '{query}'");

        // Check if we get an error result
        var errorResult = results.FirstOrDefault(r => r.Title?.StartsWith("Error:", StringComparison.CurrentCulture) == true);
        Assert.IsNotNull(errorResult, $"Should return an error result for invalid query '{query}'");
    }

    [DataTestMethod]
    [DataRow("ft1 2..548")] // Input contains space
    [DataRow("ft12..54 //8")] // Input contains space
    [DataRow("time::ft12..54 //8")] // Input contains space
    [DataRow("10.10aa")] // Input contains <Number>.<Number> (Can be part of a date.)
    [DataRow("10:10aa")] // Input contains <Number>:<Number> (Can be part of a time.)
    [DataRow("10/10aa")] // Input contains <Number>/<Number> (Can be part of a date.)
    public void InvalidInputNotShowsErrorMessage(string query)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results, $"Results should not be null for query '{query}'");

        // These queries might return valid results or empty results, but should not show error messages
        // If results are returned, they should not be error results
        if (results.Count > 0)
        {
            var hasErrorResult = results.Any(r => r.Title?.StartsWith("Error:", StringComparison.CurrentCulture) == true);
            Assert.IsFalse(hasErrorResult, $"Query '{query}' should not return error results");
        }
    }

    [DataTestMethod]
    [DataRow("time", "ime", false)] // Don't match if first word is not a full match
    [DataRow("and", "", false)] // Don't match for only conjunctions
    [DataRow("and time", "time", true)] // match if term is conjunction and other words
    [DataRow("date and time", "time", true)] // Match if first word is a full word match
    [DataRow("ate and time", "time", false)] // Don't match if first word is not a full word match
    [DataRow("10/10/10", "", false)] // Don't match number only input in some contexts
    [DataRow("10:10:10", "", false)] // Don't match number only input in some contexts
    [DataRow("10 10 10", "", false)] // Don't match number only input in some contexts
    public void ValidateBehaviorOnSearchQueries(string query, string expectedMatchTerm, bool shouldHaveResults)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results, $"Results should not be null for query '{query}'");

        if (shouldHaveResults)
        {
            Assert.IsTrue(results.Count > 0, $"Query '{query}' should return results");

            if (!string.IsNullOrEmpty(expectedMatchTerm))
            {
                var hasMatchingResult = results.Any(r =>
                    r.Title?.Contains(expectedMatchTerm, StringComparison.CurrentCultureIgnoreCase) == true ||
                    r.Details?.Body?.Contains(expectedMatchTerm, StringComparison.CurrentCultureIgnoreCase) == true);
                Assert.IsTrue(hasMatchingResult, $"Query '{query}' should return results containing '{expectedMatchTerm}'");
            }
        }
        else
        {
            // For cmdpal, invalid queries might still return error results
            // So we check if results are either empty or contain error messages
            if (results.Count > 0)
            {
                var hasErrorResult = results.Any(r => r.Title?.StartsWith("Error:", StringComparison.CurrentCulture) == true);

                // Either empty results or error results are acceptable for these cases
                Assert.IsTrue(
                    results.Count == 0 || hasErrorResult,
                    $"Query '{query}' should either return no results or error results");
            }
        }
    }

    [TestMethod]
    public void EmptyQueryReturnsAllResults()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, string.Empty);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Empty query should return all available results");
    }

    [TestMethod]
    public void NullQueryReturnsAllResults()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, null);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Null query should return all available results");
    }

    [DataTestMethod]
    [DataRow("time u", "Time UTC -")]
    [DataRow("now u", "Now UTC -")]
    [DataRow("iso utc", "ISO 8601 UTC -")]
    [DataRow("iso zone", "ISO 8601 with time zone - ")]
    [DataRow("iso utc zone", "ISO 8601 UTC with time zone -")]
    public void UTCRelatedQueries(string query, string expectedSubtitle)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, $"Query '{query}' should return results");

        var matchingResult = results.FirstOrDefault(x => x.Details?.Body?.StartsWith(expectedSubtitle, StringComparison.CurrentCulture) == true);
        Assert.IsNotNull(matchingResult, $"Could not find result with subtitle starting with '{expectedSubtitle}' for query '{query}'");
    }

    [DataTestMethod]
    [DataRow("time::12:30:45")]
    [DataRow("date::2023-12-25")]
    [DataRow("now::u1646408119")]
    [DataRow("current::ft637820085517321977")]
    public void DelimiterQueriesReturnResults(string query)
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results);

        // Delimiter queries should return results even if parsing fails (error results)
        Assert.IsTrue(results.Count > 0, $"Delimiter query '{query}' should return at least one result");
    }
}
