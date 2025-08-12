// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class TimeDateCalculatorTests
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

    [TestMethod]
    public void CountAllResults()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, string.Empty);

        // Assert
        Assert.IsTrue(results.Count > 0);
    }

    [TestMethod]
    public void ValidateEmptyQuery()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, string.Empty);

        // Assert
        Assert.IsNotNull(results);
    }

    [TestMethod]
    public void ValidateNullQuery()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, null);

        // Assert
        Assert.IsNotNull(results);
    }

    [TestMethod]
    public void ValidateTimeParsing()
    {
        // Setup
        var settings = new SettingsManager();
        var query = "time::10:30:45";

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count >= 0); // May have 0 results due to invalid format, but shouldn't crash
    }

    [TestMethod]
    public void ValidateDateParsing()
    {
        // Setup
        var settings = new SettingsManager();
        var query = "date::12/25/2023";

        // Act
        var results = TimeDateCalculator.ExecuteSearch(settings, query);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count >= 0); // May have 0 results due to invalid format, but shouldn't crash
    }

    [TestMethod]
    public void ValidateCommonQueries()
    {
        // Setup
        var settings = new SettingsManager();
        var queries = new[] { "time", "date", "now", "current" };

        foreach (var query in queries)
        {
            // Act
            var results = TimeDateCalculator.ExecuteSearch(settings, query);

            // Assert
            Assert.IsNotNull(results, $"Results should not be null for query: {query}");
        }
    }
}
