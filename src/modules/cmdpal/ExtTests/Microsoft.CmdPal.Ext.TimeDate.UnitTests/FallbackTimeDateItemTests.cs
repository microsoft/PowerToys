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

    [TestMethod]
    public void FallbackItemInitializationTest()
    {
        // Setup
        var settingsManager = new SettingsManager();

        // Act
        var fallbackItem = new FallbackTimeDateItem(settingsManager);

        // Assert
        Assert.IsNotNull(fallbackItem);
        Assert.IsNotNull(fallbackItem.DisplayTitle);
    }

    [TestMethod]
    public void UpdateQueryTest()
    {
        // Setup
        var settingsManager = new SettingsManager();
        var fallbackItem = new FallbackTimeDateItem(settingsManager);

        // Act & Assert - Test that UpdateQuery doesn't throw exceptions
        try
        {
            fallbackItem.UpdateQuery("time");
            fallbackItem.UpdateQuery("date");
            fallbackItem.UpdateQuery("now");
            fallbackItem.UpdateQuery("week");
            fallbackItem.UpdateQuery(string.Empty);
            fallbackItem.UpdateQuery(null);
        }
        catch (Exception ex)
        {
            Assert.Fail($"UpdateQuery should not throw exceptions: {ex.Message}");
        }
    }

    [TestMethod]
    public void EmptyQueryTest()
    {
        // Setup
        var settingsManager = new SettingsManager();
        var fallbackItem = new FallbackTimeDateItem(settingsManager);

        // Act
        fallbackItem.UpdateQuery(string.Empty);

        // Assert
        Assert.IsNotNull(fallbackItem.Title);
        Assert.IsNotNull(fallbackItem.Subtitle);
    }

    [TestMethod]
    public void NullQueryTest()
    {
        // Setup
        var settingsManager = new SettingsManager();
        var fallbackItem = new FallbackTimeDateItem(settingsManager);

        // Act
        fallbackItem.UpdateQuery(null);

        // Assert
        Assert.IsNotNull(fallbackItem.Title);
        Assert.IsNotNull(fallbackItem.Subtitle);
    }
}
