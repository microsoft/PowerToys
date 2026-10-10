// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class BatteryRefreshRateServiceTests
{
    [TestMethod]
    public void Battery_DefaultUsesLowestRate_AndRestoresOriginalOnAC()
    {
        var monitor = CreateMonitor();
        var external = CreateMonitor();
        external.Id = "external";
        external.CommunicationMethod = "DDC/CI";
        var writes = new List<int>();
        var service = new BatteryRefreshRateService((_, rate) =>
        {
            writes.Add(rate);
            return MonitorOperationResult.Success();
        });

        service.Apply(new[] { monitor, external }, BatteryRefreshRateMode.OnBattery, true, 0);
        Assert.AreEqual(60, monitor.CurrentRefreshRate);
        Assert.AreEqual(144, external.CurrentRefreshRate);

        // Repeated notifications and a changed preset must retain the original AC rate.
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 0);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 120);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, false, 120);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
        CollectionAssert.AreEqual(new List<int> { 60, 120, 144 }, writes);
    }

    [TestMethod]
    public void DisablingOnBattery_RestoresOriginalRate()
    {
        var monitor = CreateMonitor();
        var service = new BatteryRefreshRateService((_, _) => MonitorOperationResult.Success());

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.Off, true, 0);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 120);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.Off, true, 120);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
    }

    [TestMethod]
    public void UnsupportedPreset_FallsBackToLowest_AndReconnectRestoresOriginal()
    {
        var monitor = CreateMonitor();
        var service = new BatteryRefreshRateService((_, _) => MonitorOperationResult.Success());

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 240);
        Assert.AreEqual(60, monitor.CurrentRefreshRate);
        service.Apply(System.Array.Empty<Monitor>(), BatteryRefreshRateMode.OnBattery, false, 240);
        var rediscovered = CreateMonitor();
        rediscovered.CurrentRefreshRate = 60;
        service.Apply(new[] { rediscovered }, BatteryRefreshRateMode.OnBattery, false, 240);
        Assert.AreEqual(144, rediscovered.CurrentRefreshRate);
    }

    [TestMethod]
    public void FailedWrite_DoesNotChangeObservationOrSaveRestoreRate()
    {
        var monitor = CreateMonitor();
        var writes = 0;
        var service = new BatteryRefreshRateService((_, _) =>
        {
            writes++;
            return MonitorOperationResult.Failure("Test failure");
        });

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 0);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, false, 0);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
        Assert.AreEqual(1, writes);
    }

    [TestMethod]
    public void LiveModeRead_OnlyTouchesBuiltInPanel_AndRestoresLiveOriginalRate()
    {
        var monitor = CreateMonitor();
        var external = CreateMonitor();
        external.Id = "external";
        external.CommunicationMethod = "DDC/CI";
        var actualRate = 120;
        var reads = new List<string>();
        var service = new BatteryRefreshRateService(
            (_, rate) =>
            {
                actualRate = rate;
                return MonitorOperationResult.Success();
            },
            panel =>
            {
                reads.Add(panel.Id);
                return actualRate;
            });

        service.Apply(new[] { monitor, external }, BatteryRefreshRateMode.Off, false, 0);
        Assert.AreEqual(0, reads.Count);
        service.Apply(new[] { monitor, external }, BatteryRefreshRateMode.OnBattery, true, 0);
        Assert.AreEqual(60, actualRate);
        service.Apply(new[] { monitor, external }, BatteryRefreshRateMode.OnBattery, false, 0);
        Assert.AreEqual(120, actualRate);
        Assert.AreEqual(120, monitor.CurrentRefreshRate);
        CollectionAssert.AreEqual(new List<string> { "internal", "internal" }, reads);
    }

    [TestMethod]
    public void UnavailablePanel_IsSkippedUntilRescanReconciles()
    {
        var monitor = CreateMonitor();
        var actualRate = -1;
        var writes = 0;
        var service = new BatteryRefreshRateService(
            (_, rate) =>
            {
                writes++;
                actualRate = rate;
                return MonitorOperationResult.Success();
            },
            _ => actualRate);

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 0);
        Assert.AreEqual(0, writes);
        actualRate = 144;
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 0);
        Assert.AreEqual(60, actualRate);
        Assert.AreEqual(1, writes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnergySaverMode_OnlyLowersWhileSaverIsOn_AndRestoresWhenOff(bool onBattery)
    {
        var monitor = CreateMonitor();
        var service = new BatteryRefreshRateService((_, _) => MonitorOperationResult.Success());

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnEnergySaverMode, onBattery, 0);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnEnergySaverMode, onBattery, 0, energySaving: true);
        Assert.AreEqual(60, monitor.CurrentRefreshRate);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnEnergySaverMode, onBattery, 0);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
    }

    [TestMethod]
    public void ModeChange_RestoresWhenNewConditionIsInactive()
    {
        var monitor = CreateMonitor();
        var service = new BatteryRefreshRateService((_, _) => MonitorOperationResult.Success());
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, true, 0);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnEnergySaverMode, true, 0);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBattery, false, 0, energySaving: true);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void BatteryAndEnergySaver_RequiresBothConditions_AndRestoresWhenEitherEnds(bool onBattery, bool energySaving)
    {
        var monitor = CreateMonitor();
        var service = new BatteryRefreshRateService((_, _) => MonitorOperationResult.Success());

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBatteryAndEnergySaver, onBattery, 0, energySaving);
        Assert.AreEqual(onBattery && energySaving ? 60 : 144, monitor.CurrentRefreshRate);

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBatteryAndEnergySaver, true, 0, energySaving: true);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBatteryAndEnergySaver, false, 0, energySaving: true);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);

        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBatteryAndEnergySaver, true, 0, energySaving: true);
        service.Apply(new[] { monitor }, BatteryRefreshRateMode.OnBatteryAndEnergySaver, true, 0, energySaving: false);
        Assert.AreEqual(144, monitor.CurrentRefreshRate);
    }

    [TestMethod]
    [DataRow("{}", BatteryRefreshRateMode.Off)]
    [DataRow("{\"battery_refresh_rate_enabled\":true}", BatteryRefreshRateMode.OnBattery)]
    [DataRow("{\"battery_refresh_rate_enabled\":true,\"battery_refresh_rate_mode\":0}", BatteryRefreshRateMode.Off)]
    [DataRow("{\"battery_refresh_rate_mode\":2,\"battery_refresh_rate_enabled\":true}", BatteryRefreshRateMode.OnEnergySaverMode)]
    [DataRow("{\"battery_refresh_rate_mode\":3}", BatteryRefreshRateMode.OnBatteryAndEnergySaver)]
    public void Settings_MigrateLegacyToggleAndHonorExplicitMode(string json, BatteryRefreshRateMode expected)
    {
        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(json)!;
        Assert.AreEqual(expected, properties.BatteryRefreshRateMode);
        var serialized = JsonSerializer.Serialize(properties);
        Assert.IsFalse(serialized.Contains("battery_refresh_rate_enabled", System.StringComparison.Ordinal));
        Assert.AreEqual(expected, JsonSerializer.Deserialize<PowerDisplayProperties>(serialized)!.BatteryRefreshRateMode);
    }

    [TestMethod]
    [DataRow(null, false, false)]
    [DataRow(null, true, true)]
    [DataRow(0u, false, false)]
    [DataRow(0u, true, false)]
    [DataRow(1u, false, true)]
    [DataRow(2u, false, true)]
    [DataRow(3u, false, false)]
    public void EnergySaverStatus_Windows10FallbackAndWindows11States(uint? status, bool legacyActive, bool expected)
    {
        Assert.AreEqual(expected, BatteryRefreshRateService.IsEnergySaverActive(status, legacyActive));
    }

    private static Monitor CreateMonitor() => new()
    {
        Id = "internal",
        CommunicationMethod = "WMI",
        CurrentRefreshRate = 144,
        AvailableRefreshRates = new[] { 144, 60, 120 },
    };
}
