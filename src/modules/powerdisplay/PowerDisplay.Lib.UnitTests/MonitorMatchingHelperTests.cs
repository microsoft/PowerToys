// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Unit tests for MonitorMatchingHelper class.
/// Tests parsing logic for monitor numbers and device matching.
/// </summary>
[TestClass]
public class MonitorMatchingHelperTests
{
    [TestMethod]
    [DataRow(@"\\.\DISPLAY1", 1)]
    [DataRow(@"\\.\DISPLAY2", 2)]
    [DataRow(@"\\.\DISPLAY10", 10)]
    [DataRow(@"\\.\DISPLAY99", 99)]
    public void ParseDisplayNumber_ValidAdapterName_ReturnsCorrectNumber(string adapterName, int expected)
    {
        // Act
        var result = MonitorMatchingHelper.ParseDisplayNumber(adapterName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("DISPLAY1", 1)]
    [DataRow("DISPLAY2", 2)]
    [DataRow("display3", 3)]
    [DataRow("Display10", 10)]
    public void ParseDisplayNumber_WithoutPrefix_ReturnsCorrectNumber(string adapterName, int expected)
    {
        // Act
        var result = MonitorMatchingHelper.ParseDisplayNumber(adapterName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("", 0)]
    [DataRow("   ", 0)]
    public void ParseDisplayNumber_NullOrEmpty_ReturnsZero(string? adapterName, int expected)
    {
        // Act
        var result = MonitorMatchingHelper.ParseDisplayNumber(adapterName!);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("MONITOR1", 0)]
    [DataRow("SCREEN1", 0)]
    [DataRow("InvalidName", 0)]
    [DataRow(@"\\.\MONITOR1", 0)]
    public void ParseDisplayNumber_NoDisplayKeyword_ReturnsZero(string adapterName, int expected)
    {
        // Act
        var result = MonitorMatchingHelper.ParseDisplayNumber(adapterName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(@"\\.\DISPLAY", 0)]
    [DataRow("DISPLAY", 0)]
    [DataRow("DISPLAYabc", 0)]
    public void ParseDisplayNumber_NoNumberAfterDisplay_ReturnsZero(string adapterName, int expected)
    {
        // Act
        var result = MonitorMatchingHelper.ParseDisplayNumber(adapterName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_0", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\LGD05E5\4&abcdef12&0&UID12345_0", "4&abcdef12&0&UID12345")]
    public void ExtractDeviceInstancePath_ValidInstanceNameWithSuffix_ReturnsPathWithoutSuffix(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\TEST\devicepath", "devicepath")]
    public void ExtractDeviceInstancePath_ValidInstanceNameWithoutSuffix_ReturnsPath(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ExtractDeviceInstancePath_NullOrEmpty_ReturnsNull(string? instanceName)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName!);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    [DataRow("NoBackslashInName")]
    [DataRow("SingleSegment")]
    public void ExtractDeviceInstancePath_NoBackslash_ReturnsNull(string instanceName)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    [DataRow(@"DISPLAY\BOE0900\")]
    public void ExtractDeviceInstancePath_TrailingBackslash_ReturnsNull(string instanceName)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_MatchingDevice_ReturnsMonitorNumber()
    {
        // Arrange
        var instanceName = @"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_0";
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY1",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID265988#{some-guid}",
            },
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY2",
                DeviceKey = @"\\?\DISPLAY#DELL#4&other&0&UID999#{some-guid}",
            },
        };

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_SecondDevice_ReturnsCorrectNumber()
    {
        // Arrange
        var instanceName = @"DISPLAY\DELL\4&other&0&UID999_0";
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY1",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID265988#{some-guid}",
            },
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY2",
                DeviceKey = @"\\?\DISPLAY#DELL#4&other&0&UID999#{some-guid}",
            },
        };

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_NoMatchingDevice_ReturnsZero()
    {
        // Arrange
        var instanceName = @"DISPLAY\UNKNOWN\nonexistent_0";
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY1",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID265988#{some-guid}",
            },
        };

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_EmptyDeviceList_ReturnsZero()
    {
        // Arrange
        var instanceName = @"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_0";
        var displayDevices = new List<DisplayDeviceInfo>();

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_InvalidInstanceName_ReturnsZero()
    {
        // Arrange
        var instanceName = "InvalidInstanceName";
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY1",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID265988#{some-guid}",
            },
        };

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_CaseInsensitiveMatch_ReturnsMonitorNumber()
    {
        // Arrange
        var instanceName = @"DISPLAY\boe0900\4&10FD3AB1&0&uid265988_0";
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY1",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID265988#{some-guid}",
            },
        };

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(1, result);
    }

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
