// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
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
    public void GetMonitorKey_WithMonitor_ReturnsId()
    {
        // Arrange
        var monitor = new Monitor { Id = "DDC_GSM5C6D_1", Name = "LG Monitor" };

        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(monitor);

        // Assert
        Assert.AreEqual("DDC_GSM5C6D_1", result);
    }

    [TestMethod]
    public void GetMonitorKey_NullMonitor_ReturnsEmptyString()
    {
        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(null);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetMonitorKey_EmptyId_ReturnsEmptyString()
    {
        // Arrange
        var monitor = new Monitor { Id = string.Empty, Name = "Display Name" };

        // Act
        var result = MonitorMatchingHelper.GetMonitorKey(monitor);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void AreMonitorsSame_SameId_ReturnsTrue()
    {
        // Arrange
        var monitor1 = new Monitor { Id = "DDC_GSM5C6D_1", Name = "Monitor 1" };
        var monitor2 = new Monitor { Id = "DDC_GSM5C6D_1", Name = "Monitor 2" };

        // Act
        var result = MonitorMatchingHelper.AreMonitorsSame(monitor1, monitor2);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void AreMonitorsSame_DifferentId_ReturnsFalse()
    {
        // Arrange
        var monitor1 = new Monitor { Id = "DDC_GSM5C6D_1", Name = "Monitor 1" };
        var monitor2 = new Monitor { Id = "DDC_GSM5C6D_2", Name = "Monitor 2" };

        // Act
        var result = MonitorMatchingHelper.AreMonitorsSame(monitor1, monitor2);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void AreMonitorsSame_NullMonitor_ReturnsFalse()
    {
        // Arrange
        var monitor1 = new Monitor { Id = "DDC_GSM5C6D_1", Name = "Monitor 1" };

        // Act
        var result = MonitorMatchingHelper.AreMonitorsSame(monitor1, null!);

        // Assert
        Assert.IsFalse(result);
    }
}
