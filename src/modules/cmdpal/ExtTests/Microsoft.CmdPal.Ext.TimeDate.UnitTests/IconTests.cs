// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public class IconTests
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
    public void TimeDateCommandsProvider_HasIcon()
    {
        // Setup
        var provider = new TimeDateCommandsProvider();

        // Act
        var icon = provider.Icon;

        // Assert
        Assert.IsNotNull(icon, "Provider should have an icon");
    }

    [TestMethod]
    public void TimeDateCommandsProvider_TopLevelCommands_HaveIcons()
    {
        // Setup
        var provider = new TimeDateCommandsProvider();

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0, "Should have at least one top-level command");

        foreach (var command in commands)
        {
            Assert.IsNotNull(command.Icon, "Each command should have an icon");
        }
    }

    [TestMethod]
    public void AvailableResults_HaveIcons()
    {
        // Setup
        var settings = new SettingsManager();

        // Act
        var results = AvailableResultsList.GetList(true, settings);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Should have results");

        foreach (var result in results)
        {
            Assert.IsNotNull(result.GetIconInfo(), $"Result '{result.Label}' should have an icon");
        }
    }

    [TestMethod]
    public void ResultHelper_CreateListItem_PreservesIcon()
    {
        // Setup
        var availableResult = new AvailableResult
        {
            Label = "Test Label",
            Value = "Test Value",
            IconType = ResultIconType.Time,
        };

        // Act
        var listItem = availableResult.ToListItem();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.IsNotNull(listItem.Icon, "ListItem should preserve the icon from AvailableResult");
    }

    [TestMethod]
    public void Icons_AreNotEmpty()
    {
        // Setup
        var settings = new SettingsManager();
        var results = AvailableResultsList.GetList(true, settings);

        // Act & Assert
        foreach (var result in results)
        {
            Assert.IsNotNull(result.GetIconInfo(), $"Result '{result.Label}' should have an icon");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetIconInfo().ToString()), $"Icon for '{result.Label}' should not be empty");
        }
    }
}
