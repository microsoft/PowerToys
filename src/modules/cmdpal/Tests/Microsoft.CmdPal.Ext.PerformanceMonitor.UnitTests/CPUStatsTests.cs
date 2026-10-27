// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.System.Power;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class CPUStatsTests
{
    [TestMethod]
    public void BaseSpeedIsTheFastestClustersRating()
    {
        // Two core clusters rated 2808 and 4004 MHz. Current speeds don't matter.
        PROCESSOR_POWER_INFORMATION[] processors =
        [
            new() { Number = 0, MaxMhz = 2808, CurrentMhz = 2808 },
            new() { Number = 1, MaxMhz = 2808, CurrentMhz = 1200 },
            new() { Number = 2, MaxMhz = 4004, CurrentMhz = 3100 },
            new() { Number = 3, MaxMhz = 4004, CurrentMhz = 4400 },
        ];

        Assert.AreEqual(4004u, CPUStats.GetBaseSpeed(processors));
    }

    [TestMethod]
    public void BaseSpeedIsUnknownWithoutRatings()
    {
        PROCESSOR_POWER_INFORMATION[] unrated = [new() { Number = 0 }, new() { Number = 1 }];

        Assert.AreEqual(0u, CPUStats.GetBaseSpeed(unrated));
        Assert.AreEqual(0u, CPUStats.GetBaseSpeed([]));
    }
}
