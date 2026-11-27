// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        using var file = new SettingsTestFile();
        const string adapterId = "network-interface:11111111-1111-1111-1111-111111111111";
        var settings = new SettingsManager(file.FilePath);
        Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, settings.DefaultNetworkAdapterId);

        settings.SetDefaultNetworkAdapterId(adapterId);

        Assert.AreEqual(adapterId, new SettingsManager(file.FilePath).DefaultNetworkAdapterId);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NetworkAdapterFilters_DefaultsIncludeVirtualAndExcludeDisconnectedAdapters(bool existingSettings)
    {
        using var file = new SettingsTestFile();
        if (existingSettings)
        {
            var savedSettings = """
                {
                    "performanceMonitor.NetworkSpeedUnit": "BytesPerSecond",
                    "performanceMonitor.DefaultNetworkAdapterId": "all-physical-network-adapters"
                }
                """;
            File.WriteAllText(file.FilePath, savedSettings);
        }

        var settings = new SettingsManager(file.FilePath);

        Assert.IsFalse(settings.ExcludeVirtualNetworkAdapters);
        Assert.IsTrue(settings.ExcludeDisconnectedNetworkAdapters);
        Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, settings.DefaultNetworkAdapterId);
        if (existingSettings)
        {
            Assert.AreEqual(SpeedUnit.BytesPerSecond, settings.NetworkSpeedUnit);
        }
    }

    [TestMethod]
    public void NetworkAdapterFilters_RoundTripWithoutLosingDefaultAdapterOrSpeedUnits()
    {
        using var file = new SettingsTestFile();
        const string adapterId = "network-interface:22222222-2222-2222-2222-222222222222";
        var settings = new SettingsManager(file.FilePath);
        settings.Settings.Update("""
            {
                "performanceMonitor.ExcludeVirtualNetworkAdapters": "true",
                "performanceMonitor.ExcludeDisconnectedNetworkAdapters": "false",
                "performanceMonitor.NetworkSpeedUnit": "BytesPerSecond",
                "performanceMonitor.DiskSpeedUnit": "BinaryBytesPerSecond"
            }
            """);
        settings.SetDefaultNetworkAdapterId(adapterId);

        var reloadedSettings = new SettingsManager(file.FilePath);
        Assert.IsTrue(reloadedSettings.ExcludeVirtualNetworkAdapters);
        Assert.IsFalse(reloadedSettings.ExcludeDisconnectedNetworkAdapters);
        Assert.AreEqual(adapterId, reloadedSettings.DefaultNetworkAdapterId);
        Assert.AreEqual(SpeedUnit.BytesPerSecond, reloadedSettings.NetworkSpeedUnit);
        Assert.AreEqual(SpeedUnit.BinaryBytesPerSecond, reloadedSettings.DiskSpeedUnit);
    }
}
