// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Tests specifically for UWP application functionality
/// </summary>
[TestClass]
public class UWPApplicationTests
{
    [TestMethod]
    public void MockUWPApplication_BasicProperties_Work()
    {
        // Arrange & Act
        var app = TestDataHelper.CreateTestUWPApplication(
            "Windows Calculator",
            "Microsoft.WindowsCalculator_8wekyb3d8bbwe",
            true);

        // Assert
        Assert.IsNotNull(app);
        Assert.AreEqual("Windows Calculator", app.DisplayName);
        Assert.AreEqual("Windows Calculator", app.Name);
        Assert.AreEqual("Microsoft.WindowsCalculator_8wekyb3d8bbwe", app.UserModelId);
        Assert.IsTrue(app.Enabled);
        Assert.IsNotNull(app.Package);
    }

    [TestMethod]
    public void MockUWPApplication_ToAppItem_Works()
    {
        // Arrange
        var app = TestDataHelper.CreateTestUWPApplication(
            "Mail",
            "microsoft.windowscommunicationsapps_8wekyb3d8bbwe");

        // Act
        var appItem = app.ToAppItem();

        // Assert
        Assert.IsNotNull(appItem);
        Assert.AreEqual("Mail", appItem.Name);
        Assert.AreEqual("microsoft.windowscommunicationsapps_8wekyb3d8bbwe", appItem.UserModelId);
        Assert.IsTrue(appItem.IsPackaged);
        Assert.AreEqual("Packaged Application", appItem.Type);
    }

    [TestMethod]
    public void MockUWPApplication_GetAppIdentifier_ReturnsUserModelId()
    {
        // Arrange
        var userModelId = "TestPublisher.TestApp_1.0.0.0_neutral__8wekyb3d8bbwe";
        var app = TestDataHelper.CreateTestUWPApplication(
            "Test App",
            userModelId);

        // Act
        var identifier = app.GetAppIdentifier();

        // Assert
        Assert.AreEqual(userModelId, identifier);
    }

    [TestMethod]
    public void MockUWPApplication_Location_ReturnsPackageLocation()
    {
        // Arrange
        var app = TestDataHelper.CreateTestUWPApplication("Test App");

        // Act
        var location = app.Location;

        // Assert
        Assert.IsNotNull(location);
        Assert.IsTrue(location.Contains("Test App"));
    }

    [TestMethod]
    public void MockUWPApplication_GetCommands_ReturnsEmptyList()
    {
        // Arrange
        var app = TestDataHelper.CreateTestUWPApplication("Test App");

        // Act
        var commands = app.GetCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.AreEqual(0, commands.Count); // Mock returns empty list
    }
}
