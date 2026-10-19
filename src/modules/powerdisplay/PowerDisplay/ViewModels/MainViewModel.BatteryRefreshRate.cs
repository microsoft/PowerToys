// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Services;
using Windows.System.Power;

using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.ViewModels;

public partial class MainViewModel
{
    private readonly BatteryRefreshRateService _batteryRefreshRateService;
    private IReadOnlyList<Monitor> _batteryRefreshRateMonitors = Array.Empty<Monitor>();
    private bool _batteryRefreshRateDisposed;

    private void OnPowerStatusChanged(object? sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Power-source changes must not wait for the display-watcher's rescan debounce.
            if (!_batteryRefreshRateDisposed && IsInitialized)
            {
                ApplyBatteryRefreshRate();
            }
        });
    }

    private void ApplyBatteryRefreshRate()
    {
        try
        {
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            var onBattery = PowerManager.PowerSupplyStatus == PowerSupplyStatus.NotPresent &&
                PowerManager.BatteryStatus != BatteryStatus.NotPresent;
            _batteryRefreshRateService.Apply(
                _batteryRefreshRateMonitors,
                settings.Properties.BatteryRefreshRateMode,
                onBattery,
                settings.Properties.BatteryRefreshRate,
                BatteryRefreshRateService.IsEnergySaverActive(
                    _displayChangeWatcher.EnergySaverStatus,
                    PowerManager.EnergySaverStatus == EnergySaverStatus.On));
        }
        catch (Exception ex)
        {
            Logger.LogError($"[BatteryRefreshRate] Failed to apply power-source settings: {ex.Message}");
        }
    }
}
