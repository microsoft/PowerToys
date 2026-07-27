// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorRestorePlannerTests
{
    private static Monitor MonitorWithValue(
        int value,
        MonitorReadFlags valueFlag,
        MonitorReadFlags readValues)
    {
        var monitor = new Monitor { ReadValues = readValues };
        switch (valueFlag)
        {
            case MonitorReadFlags.Brightness:
                monitor.CurrentBrightness = value;
                break;
            case MonitorReadFlags.Contrast:
                monitor.CurrentContrast = value;
                break;
            case MonitorReadFlags.Volume:
                monitor.CurrentVolume = value;
                break;
            case MonitorReadFlags.ColorTemperature:
                monitor.CurrentColorTemperature = value;
                break;
        }

        return monitor;
    }

    [DataTestMethod]
    [DataRow(MonitorReadFlags.Brightness)]
    [DataRow(MonitorReadFlags.Contrast)]
    [DataRow(MonitorReadFlags.Volume)]
    [DataRow(MonitorReadFlags.ColorTemperature)]
    public void ShouldWrite_MatchingValueWasNotRead_ReturnsTrue(MonitorReadFlags readFlag)
    {
        var monitor = MonitorWithValue(45, readFlag, MonitorReadFlags.None);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(45, monitor, readFlag, 45));
    }

    [DataTestMethod]
    [DataRow(MonitorReadFlags.Brightness)]
    [DataRow(MonitorReadFlags.Contrast)]
    [DataRow(MonitorReadFlags.Volume)]
    [DataRow(MonitorReadFlags.ColorTemperature)]
    public void ShouldWrite_MatchingValueWasRead_ReturnsFalse(MonitorReadFlags readFlag)
    {
        var monitor = MonitorWithValue(45, readFlag, readFlag);

        Assert.IsFalse(MonitorRestorePlanner.ShouldWrite(45, monitor, readFlag, 45));
    }

    [DataTestMethod]
    [DataRow(MonitorReadFlags.Brightness, MonitorReadFlags.Contrast)]
    [DataRow(MonitorReadFlags.Contrast, MonitorReadFlags.Volume)]
    [DataRow(MonitorReadFlags.Volume, MonitorReadFlags.ColorTemperature)]
    [DataRow(MonitorReadFlags.ColorTemperature, MonitorReadFlags.Brightness)]
    public void ShouldWrite_MatchingValueOnlyDifferentSettingWasRead_ReturnsTrue(
        MonitorReadFlags readFlag,
        MonitorReadFlags differentReadFlag)
    {
        var monitor = MonitorWithValue(45, readFlag, differentReadFlag);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(45, monitor, readFlag, 45));
    }

    [DataTestMethod]
    [DataRow(MonitorReadFlags.Brightness)]
    [DataRow(MonitorReadFlags.Contrast)]
    [DataRow(MonitorReadFlags.Volume)]
    [DataRow(MonitorReadFlags.ColorTemperature)]
    public void ShouldWrite_DifferentValueWasRead_ReturnsTrue(MonitorReadFlags readFlag)
    {
        var monitor = MonitorWithValue(45, readFlag, readFlag);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(60, monitor, readFlag, 60));
    }

    [DataTestMethod]
    [DataRow(MonitorReadFlags.Brightness)]
    [DataRow(MonitorReadFlags.Contrast)]
    [DataRow(MonitorReadFlags.Volume)]
    [DataRow(MonitorReadFlags.ColorTemperature)]
    public void ShouldWrite_PendingOptimisticValueDisagrees_ReturnsTrue(MonitorReadFlags readFlag)
    {
        // Hardware is known to be at 45 and the restore also wants 45, but the UI is already
        // showing 65 and will commit it once its debounce elapses. Skipping the write here would
        // let that pending commit silently overwrite the restored value.
        var monitor = MonitorWithValue(45, readFlag, readFlag);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(45, monitor, readFlag, 65));
    }
}
