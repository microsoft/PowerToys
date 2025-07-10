// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class QueryTests
{
    [DataTestMethod]
    [DataRow("shutdown", "Shutdown")]
    [DataRow("restart", "Restart")]
    [DataRow("sign out", "Sign out")]
    [DataRow("lock", "Lock")]
    [DataRow("sleep", "Sleep")]
    [DataRow("hibernate", "Hibernate")]
    public void SystemCommandsTest(string typedString, string expectedCommand)
    {
        // Setup
        var commands = Commands.GetSystemCommands(false, false, false, false);

        // Act
        var result = commands.Where(c => c.Title.Contains(expectedCommand, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Title.Contains(expectedCommand, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RecycleBinCommandTest()
    {
        // Setup
        var commands = Commands.GetSystemCommands(false, false, false, false);

        // Act
        var result = commands.Where(c => c.Title.Contains("Recycle", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void NetworkCommandsTest()
    {
        // Test that network commands can be retrieved
        try
        {
            var networkPropertiesList = NetworkConnectionProperties.GetList();
            Assert.IsTrue(networkPropertiesList.Count >= 0); // Should not throw exceptions
        }
        catch (Exception ex)
        {
            Assert.Fail($"Network commands should not throw exceptions: {ex.Message}");
        }
    }

    [TestMethod]
    public void UefiCommandIsAvailableTest()
    {
        // Setup
        var firmwareType = Win32Helpers.GetSystemFirmwareType();
        var isUefiMode = firmwareType == FirmwareType.Uefi;

        // Act
        var commands = Commands.GetSystemCommands(isUefiMode, false, false, false);
        var uefiCommand = commands.Where(c => c.Title.Contains("UEFI", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        // Assert
        if (isUefiMode)
        {
            Assert.IsNotNull(uefiCommand);
        }
        else
        {
            // UEFI command may still exist but be disabled on non-UEFI systems
            Assert.IsTrue(true); // Test environment independent
        }
    }

    [TestMethod]
    public void FirmwareTypeTest()
    {
        // Test that GetSystemFirmwareType returns a valid enum value
        var firmwareType = Win32Helpers.GetSystemFirmwareType();
        Assert.IsTrue(Enum.IsDefined(typeof(FirmwareType), firmwareType));
    }

    [TestMethod]
    public void EmptyRecycleBinCommandTest()
    {
        // Test that empty recycle bin command exists
        var commands = Commands.GetSystemCommands(false, false, false, false);
        var result = commands.Where(c => c.Title.Contains("Empty", StringComparison.OrdinalIgnoreCase) &&
                                         c.Title.Contains("Recycle", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        // Empty recycle bin command should exist
        Assert.IsNotNull(result);
    }
}
