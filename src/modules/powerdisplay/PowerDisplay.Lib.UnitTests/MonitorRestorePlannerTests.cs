// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class MonitorRestorePlannerTests
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

    // Only ShouldWrite_MatchingValueWasRead_ReturnsFalse varies the flag across rows. It is the one
    // case where all three clauses are false, so it is the only one whose verdict depends on the
    // readFlag switch reading the right field; everywhere else a mis-mapped arm yields a default
    // that still satisfies the clause under test, and the extra rows pin nothing.
    [TestMethod]
    public void ShouldWrite_MatchingValueWasNotRead_ReturnsTrue()
    {
        var monitor = MonitorWithValue(45, MonitorReadFlags.Brightness, MonitorReadFlags.None);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(45, monitor, MonitorReadFlags.Brightness, 45));
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

    [TestMethod]
    public void ShouldWrite_MatchingValueOnlyDifferentSettingWasRead_ReturnsTrue()
    {
        var monitor = MonitorWithValue(45, MonitorReadFlags.Brightness, MonitorReadFlags.Contrast);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(45, monitor, MonitorReadFlags.Brightness, 45));
    }

    [TestMethod]
    public void ShouldWrite_DifferentValueWasRead_ReturnsTrue()
    {
        var monitor = MonitorWithValue(45, MonitorReadFlags.Contrast, MonitorReadFlags.Contrast);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(60, monitor, MonitorReadFlags.Contrast, 60));
    }

    [TestMethod]
    public void ShouldWrite_PendingOptimisticValueDisagrees_ReturnsTrue()
    {
        // Hardware is known to be at 45 and the restore also wants 45, but the UI is already
        // showing 65 and will commit it once its debounce elapses. Skipping the write here would
        // let that pending commit silently overwrite the restored value.
        var monitor = MonitorWithValue(45, MonitorReadFlags.Volume, MonitorReadFlags.Volume);

        Assert.IsTrue(MonitorRestorePlanner.ShouldWrite(45, monitor, MonitorReadFlags.Volume, 65));
    }
}
