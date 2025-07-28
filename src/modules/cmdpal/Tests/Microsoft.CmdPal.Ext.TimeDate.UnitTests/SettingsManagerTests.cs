// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class SettingsManagerTests
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
    public void SettingsManagerInitializationTest()
    {
        // Act
        var settingsManager = new SettingsManager();

        // Assert
        Assert.IsNotNull(settingsManager);
        Assert.IsNotNull(settingsManager.Settings);
    }

    [TestMethod]
    public void DefaultSettingsValidation()
    {
        // Act
        var settingsManager = new SettingsManager();

        // Assert - Check that properties are accessible
        var enableFallback = settingsManager.EnableFallbackItems;
        var timeWithSecond = settingsManager.TimeWithSecond;
        var dateWithWeekday = settingsManager.DateWithWeekday;
        var firstWeekOfYear = settingsManager.FirstWeekOfYear;
        var firstDayOfWeek = settingsManager.FirstDayOfWeek;
        var customFormats = settingsManager.CustomFormats;

        Assert.IsNotNull(customFormats);
    }

    [TestMethod]
    public void SettingsPropertiesAccessibilityTest()
    {
        // Setup
        var settingsManager = new SettingsManager();

        // Act & Assert - Verify all properties are accessible without exception
        try
        {
            _ = settingsManager.EnableFallbackItems;
            _ = settingsManager.TimeWithSecond;
            _ = settingsManager.DateWithWeekday;
            _ = settingsManager.FirstWeekOfYear;
            _ = settingsManager.FirstDayOfWeek;
            _ = settingsManager.CustomFormats;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Settings properties should be accessible: {ex.Message}");
        }
    }
}
