// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class FallbackTimeDateItemTests
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
    [DataRow("time", "12:00 PM")]
    [DataRow("date", "7/1/2025")]
    [DataRow("week", "27")]
    public void FallbackQueryTests(string query, string expectedTitle)
    {
        // Setup
        var settingsManager = new Settings();
        DateTime now = new DateTime(2025, 7, 1, 12, 0, 0); // Fixed date for testing
        var fallbackItem = new FallbackTimeDateItem(settingsManager, now);

        // Act & Assert - Test that UpdateQuery doesn't throw exceptions
        try
        {
            fallbackItem.UpdateQuery(query);
            Assert.IsTrue(
                fallbackItem.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase),
                $"Expected title to contain '{expectedTitle}', but got '{fallbackItem.Title}'");
            Assert.IsNotNull(fallbackItem.Subtitle, "Subtitle should not be null");
            Assert.IsNotNull(fallbackItem.Icon, "Icon should not be null");
        }
        catch (Exception ex)
        {
            Assert.Fail($"UpdateQuery should not throw exceptions: {ex.Message}");
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("invalid input")]
    public void InvalidQueryTests(string query)
    {
        // Setup
        var settingsManager = new Settings();
        DateTime now = new DateTime(2025, 7, 1, 12, 0, 0); // Fixed date for testing
        var fallbackItem = new FallbackTimeDateItem(settingsManager, now);

        // Act & Assert - Test that UpdateQuery doesn't throw exceptions
        try
        {
            fallbackItem.UpdateQuery(query);

            Assert.AreEqual(string.Empty, fallbackItem.Title, "Title should be empty for invalid queries");
            Assert.AreEqual(string.Empty, fallbackItem.Subtitle, "Subtitle should be empty for invalid queries");
        }
        catch (Exception ex)
        {
            Assert.Fail($"UpdateQuery should not throw exceptions: {ex.Message}");
        }
    }

    [DataTestMethod]
    public void DisableFallbackItemTest()
    {
        // Setup
        var settingsManager = new Settings(enableFallbackItems: false);
        DateTime now = new DateTime(2025, 7, 1, 12, 0, 0); // Fixed date for testing
        var fallbackItem = new FallbackTimeDateItem(settingsManager, now);

        // Act & Assert - Test that UpdateQuery doesn't throw exceptions
        try
        {
            fallbackItem.UpdateQuery("now");

            Assert.AreEqual(string.Empty, fallbackItem.Title, "Title should be empty when disable fallback item");
            Assert.AreEqual(string.Empty, fallbackItem.Subtitle, "Subtitle should be empty when disable fallback item");
        }
        catch (Exception ex)
        {
            Assert.Fail($"UpdateQuery should not throw exceptions: {ex.Message}");
        }
    }
}
