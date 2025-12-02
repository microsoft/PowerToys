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
    [DataRow(@"DISPLAY\HPN360C\5&2c03a83e&0&UID262_1", "5&2c03a83e&0&UID262")]
    [DataRow(@"DISPLAY\DELL\4&other&0&UID999_12", "4&other&0&UID999")]
    [DataRow(@"DISPLAY\TEST\path_99", "path")]
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
    [DataRow(@"DISPLAY\TEST\path_abc", "path_abc")] // Non-digit suffix should not be removed
    [DataRow(@"DISPLAY\TEST\path_", "path_")] // Empty suffix should not be removed
    [DataRow(@"DISPLAY\TEST\path_0x1", "path_0x1")] // Mixed suffix should not be removed
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

    /// <summary>
    /// Tests that WMI instance index suffixes (_0, _1, _2, etc.) are correctly removed.
    /// WMI appends "_N" where N is the instance index to device instance paths.
    /// See: https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorid
    /// </summary>
    [TestMethod]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_0", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_1", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_2", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_9", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_10", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_99", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_100", "4&10fd3ab1&0&UID265988")]
    [DataRow(@"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_999", "4&10fd3ab1&0&UID265988")]
    public void ExtractDeviceInstancePath_WmiInstanceIndexSuffix_RemovesSuffix(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Tests real-world WMI InstanceName formats from various monitor manufacturers.
    /// These formats are documented at: https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorid
    /// </summary>
    [TestMethod]
    [DataRow(@"DISPLAY\HPN360C\5&2c03a83e&0&UID262_0", "5&2c03a83e&0&UID262")] // HP monitor
    [DataRow(@"DISPLAY\HWP2868\5&3eb7fbc&0&UID16777472_0", "5&3eb7fbc&0&UID16777472")] // HP monitor
    [DataRow(@"DISPLAY\ENC2530\4&307c4481&0&UID224795_0", "4&307c4481&0&UID224795")] // EIZO monitor
    [DataRow(@"DISPLAY\DEL0000\0&00000000&0&UID0000_0", "0&00000000&0&UID0000")] // Dell monitor
    [DataRow(@"DISPLAY\SAM0382\4&38b6bd55&0&UID198147_0", "4&38b6bd55&0&UID198147")] // Samsung monitor
    [DataRow(@"DISPLAY\GSM5C6D\5&1234abcd&0&UID999_0", "5&1234abcd&0&UID999")] // LG monitor
    [DataRow(@"DISPLAY\ACI27F6\4&deadbeef&0&UID12345_0", "4&deadbeef&0&UID12345")] // ASUS monitor
    public void ExtractDeviceInstancePath_RealWorldFormats_ExtractsCorrectly(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Tests that non-numeric suffixes are preserved (not removed).
    /// Only pure numeric suffixes after underscore should be removed.
    /// </summary>
    [TestMethod]
    [DataRow(@"DISPLAY\TEST\path_abc", "path_abc")]
    [DataRow(@"DISPLAY\TEST\path_0a", "path_0a")]
    [DataRow(@"DISPLAY\TEST\path_a0", "path_a0")]
    [DataRow(@"DISPLAY\TEST\path_0x1", "path_0x1")]
    [DataRow(@"DISPLAY\TEST\path_1x", "path_1x")]
    [DataRow(@"DISPLAY\TEST\path_", "path_")]
    [DataRow(@"DISPLAY\TEST\path_ ", "path_ ")]
    public void ExtractDeviceInstancePath_NonNumericSuffix_PreservesSuffix(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Tests paths with multiple underscores - only the last numeric suffix should be removed.
    /// </summary>
    [TestMethod]
    [DataRow(@"DISPLAY\TEST\a_b_0", "a_b")]
    [DataRow(@"DISPLAY\TEST\a_b_c_1", "a_b_c")]
    [DataRow(@"DISPLAY\TEST\path_with_underscores_99", "path_with_underscores")]
    [DataRow(@"DISPLAY\TEST\UID_265988_0", "UID_265988")]
    public void ExtractDeviceInstancePath_MultipleUnderscores_RemovesOnlyLastNumericSuffix(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Tests edge cases where underscore is at the beginning.
    /// </summary>
    [TestMethod]
    [DataRow(@"DISPLAY\TEST\_0", "_0")] // Underscore at position 0, should not be removed
    [DataRow(@"DISPLAY\TEST\_123", "_123")] // Underscore at position 0, should not be removed
    public void ExtractDeviceInstancePath_UnderscoreAtStart_PreservesPath(string instanceName, string expected)
    {
        // Act
        var result = MonitorMatchingHelper.ExtractDeviceInstancePath(instanceName);

        // Assert
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Tests that GetMonitorNumberFromWmiInstanceName works correctly with non-zero WMI instance indices.
    /// This verifies the fix for handling _1, _2, etc. suffixes instead of just _0.
    /// </summary>
    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_WithInstanceIndex1_ReturnsMonitorNumber()
    {
        // Arrange - Using _1 suffix instead of _0
        var instanceName = @"DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_1";
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

    /// <summary>
    /// Tests matching with multi-digit WMI instance index.
    /// </summary>
    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_WithInstanceIndex12_ReturnsMonitorNumber()
    {
        // Arrange - Using _12 suffix
        var instanceName = @"DISPLAY\DELL\4&abcdef&0&UID999_12";
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY3",
                DeviceKey = @"\\?\DISPLAY#DELL#4&abcdef&0&UID999#{guid}",
            },
        };

        // Act
        var result = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);

        // Assert
        Assert.AreEqual(3, result);
    }

    /// <summary>
    /// Tests that multiple monitors with different WMI instance indices match correctly.
    /// Simulates a scenario with duplicate monitor models where WMI assigns different indices.
    /// </summary>
    [TestMethod]
    public void GetMonitorNumberFromWmiInstanceName_MultipleMonitorsSameModel_MatchesCorrectly()
    {
        // Arrange - Two monitors of same model but different UIDs
        var displayDevices = new List<DisplayDeviceInfo>
        {
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY1",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID100#{guid}",
            },
            new DisplayDeviceInfo
            {
                AdapterName = @"\\.\DISPLAY2",
                DeviceKey = @"\\?\DISPLAY#BOE0900#4&10fd3ab1&0&UID200#{guid}",
            },
        };

        // Act & Assert - First monitor with _0 suffix
        var result1 = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(
            @"DISPLAY\BOE0900\4&10fd3ab1&0&UID100_0", displayDevices);
        Assert.AreEqual(1, result1);

        // Act & Assert - Second monitor with _0 suffix (different UID)
        var result2 = MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(
            @"DISPLAY\BOE0900\4&10fd3ab1&0&UID200_0", displayDevices);
        Assert.AreEqual(2, result2);
    }
}
