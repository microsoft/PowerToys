// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Unit tests for MonitorMatchingHelper class.
/// Tests monitor key generation and matching logic.
/// </summary>
[TestClass]
public class MonitorMatchingHelperTests
{
    [TestMethod]
    public void GetMonitorKey_WithHardwareId_ReturnsHardwareId()
    {
        // Arrange
        var hardwareId = "HW_ID_123";
        var internalName = "Internal_Name";
        var name = "Display Name";

        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(hardwareId, internalName, name);

        // Assert
        Assert.AreEqual(hardwareId, result);
    }

    [TestMethod]
    public void GetMonitorKey_NoHardwareId_ReturnsInternalName()
    {
        // Arrange
        string? hardwareId = null;
        var internalName = "Internal_Name";
        var name = "Display Name";

        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(hardwareId, internalName, name);

        // Assert
        Assert.AreEqual(internalName, result);
    }

    [TestMethod]
    public void GetMonitorKey_NoHardwareIdOrInternalName_ReturnsName()
    {
        // Arrange
        string? hardwareId = null;
        string? internalName = null;
        var name = "Display Name";

        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(hardwareId, internalName, name);

        // Assert
        Assert.AreEqual(name, result);
    }

    [TestMethod]
    public void GetMonitorKey_AllNull_ReturnsEmptyString()
    {
        // Arrange & Act
        var result = MonitorMatchingHelper.GetMonitorKey(null, null, null);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetMonitorKey_EmptyHardwareId_FallsBackToInternalName()
    {
        // Arrange
        var hardwareId = string.Empty;
        var internalName = "Internal_Name";
        var name = "Display Name";

        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(hardwareId, internalName, name);

        // Assert
        Assert.AreEqual(internalName, result);
    }
}
