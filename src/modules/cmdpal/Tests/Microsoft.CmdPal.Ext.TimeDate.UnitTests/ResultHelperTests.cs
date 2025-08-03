// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class ResultHelperTests
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

    [TestMethod]
    public void ResultHelper_CreateListItem_ReturnsValidItem()
    {
        // Setup
        var availableResult = new AvailableResult
        {
            Label = "Test Label",
            Value = "Test Value",
        };

        // Act
        var listItem = availableResult.ToListItem();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.AreEqual("Test Value", listItem.Title);
        Assert.AreEqual("Test Label", listItem.Subtitle);
    }

    [TestMethod]
    public void ResultHelper_CreateListItem_HandlesNullInput()
    {
        AvailableResult availableResult = null;

        // Act & Assert
        Assert.ThrowsException<System.NullReferenceException>(() => availableResult.ToListItem());
    }

    [TestMethod]
    public void ResultHelper_CreateListItem_HandlesEmptyValues()
    {
        // Setup
        var availableResult = new AvailableResult
        {
            Label = string.Empty,
            Value = string.Empty,
        };

        // Act
        var listItem = availableResult.ToListItem();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.AreEqual("Copy", listItem.Title);
        Assert.AreEqual(string.Empty, listItem.Subtitle);
    }

    [TestMethod]
    public void ResultHelper_CreateListItem_WithIcon()
    {
        // Setup
        var availableResult = new AvailableResult
        {
            Label = "Test Label",
            Value = "Test Value",
            IconType = ResultIconType.Date,
        };

        // Act
        var listItem = availableResult.ToListItem();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.AreEqual("Test Value", listItem.Title);
        Assert.AreEqual("Test Label", listItem.Subtitle);
        Assert.IsNotNull(listItem.Icon);
    }

    [TestMethod]
    public void ResultHelper_CreateListItem_WithLongText()
    {
        // Setup
        var longText = new string('A', 1000);
        var availableResult = new AvailableResult
        {
            Label = longText,
            Value = longText,
        };

        // Act
        var listItem = availableResult.ToListItem();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.AreEqual(longText, listItem.Title);
        Assert.AreEqual(longText, listItem.Subtitle);
    }

    [TestMethod]
    public void ResultHelper_CreateListItem_WithSpecialCharacters()
    {
        // Setup
        var specialText = "Test & < > \" ' \n \t";
        var availableResult = new AvailableResult
        {
            Label = specialText,
            Value = specialText,
        };

        // Act
        var listItem = availableResult.ToListItem();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.AreEqual(specialText, listItem.Title);
        Assert.AreEqual(specialText, listItem.Subtitle);
    }
}
