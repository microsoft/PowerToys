// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class SettingsManagerTests
{
    [TestMethod]
    public void DefaultNetworkAdapterId_RoundTripsThroughSettingsFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PerformanceMonitorSettingsTests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "performanceMonitor.settings.json");
        var adapterId = "network-interface:11111111-1111-1111-1111-111111111111";

        try
        {
            Directory.CreateDirectory(directory);

            var settings = new SettingsManager(filePath);
            Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, settings.DefaultNetworkAdapterId);

            settings.SetDefaultNetworkAdapterId(adapterId);

            var reloadedSettings = new SettingsManager(filePath);
            Assert.AreEqual(adapterId, reloadedSettings.DefaultNetworkAdapterId);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
