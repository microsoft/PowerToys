// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Applies a temporary battery refresh rate without persisting it as the user's normal rate.
    /// </summary>
    public sealed class BatteryRefreshRateService
    {
        private readonly Func<Monitor, int, MonitorOperationResult> _setRefreshRate;
        private readonly Func<Monitor, int>? _getRefreshRate;
        private readonly Dictionary<string, int> _originalRates = new(MonitorIdComparer.Instance);

        public BatteryRefreshRateService(
            Func<Monitor, int, MonitorOperationResult> setRefreshRate,
            Func<Monitor, int>? getRefreshRate = null)
        {
            _setRefreshRate = setRefreshRate ?? throw new ArgumentNullException(nameof(setRefreshRate));
            _getRefreshRate = getRefreshRate;
        }

        /// <summary>
        /// Uses modern Energy Saver status when available, otherwise Windows 10 Battery Saver.
        /// </summary>
        public static bool IsEnergySaverActive(uint? energySaverStatus, bool legacyBatterySaverActive)
            => energySaverStatus is null ? legacyBatterySaverActive : energySaverStatus is 1 or 2;

        public void Apply(IEnumerable<Monitor> monitors, BatteryRefreshRateMode mode, bool onBattery, int preset, bool energySaving = false)
        {
            var active = mode switch
            {
                BatteryRefreshRateMode.OnBattery => onBattery,
                BatteryRefreshRateMode.OnEnergySaverMode => energySaving,
                BatteryRefreshRateMode.OnBatteryAndEnergySaver => onBattery && energySaving,
                _ => false,
            };

            foreach (var monitor in monitors.Where(m => m.CommunicationMethod == "WMI"))
            {
                int target;
                if (active)
                {
                    if (monitor.AvailableRefreshRates.Count == 0)
                    {
                        continue;
                    }

                    target = monitor.AvailableRefreshRates.Contains(preset)
                        ? preset
                        : monitor.AvailableRefreshRates.Min();
                }
                else if (!_originalRates.TryGetValue(monitor.Id, out target))
                {
                    continue;
                }

                if (!monitor.AvailableRefreshRates.Contains(target))
                {
                    continue;
                }

                // Only query the built-in panel's current mode, not every display's mode list.
                var originalRate = _getRefreshRate?.Invoke(monitor) ?? monitor.CurrentRefreshRate;
                if (originalRate <= 0)
                {
                    continue;
                }

                if (originalRate != target)
                {
                    var result = _setRefreshRate(monitor, target);
                    if (!result.IsSuccess)
                    {
                        Logger.LogWarning($"[BatteryRefreshRate] Failed to set {target} Hz for {monitor.Id}");
                        continue;
                    }
                }

                monitor.CurrentRefreshRate = target;

                if (active)
                {
                    _originalRates.TryAdd(monitor.Id, originalRate);
                }
                else
                {
                    _originalRates.Remove(monitor.Id);
                }
            }
        }
    }
}
