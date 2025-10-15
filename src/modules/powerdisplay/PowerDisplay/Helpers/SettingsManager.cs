// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Settings manager with periodic save mechanism (write-only during runtime)
    /// </summary>
    public class SettingsManager : IDisposable
    {
        private readonly ISettingsUtils _settingsUtils;
        private readonly SemaphoreSlim _fileAccessSemaphore = new(1, 1);
        private readonly Timer _periodicSaveTimer;
        private readonly ConcurrentDictionary<string, MonitorSettings> _pendingSettings = new();
        private const string MODULE_NAME = "PowerDisplay";
        private const int PERIODIC_SAVE_INTERVAL_MS = 1000; // Check every 1 second
        private bool _disposed;
        private bool _hasPendingChanges = false;

        /// <summary>
        /// Represents all settings for a single monitor
        /// </summary>
        private class MonitorSettings
        {
            public int Brightness { get; set; }
            public int ColorTemperature { get; set; }
            public int Contrast { get; set; }
            public int Volume { get; set; }
            public bool IsDirty { get; set; } = false;
        }

        public SettingsManager(ISettingsUtils settingsUtils)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            
            // Start periodic timer that checks every second
            _periodicSaveTimer = new Timer(CheckAndSavePendingChanges, null, PERIODIC_SAVE_INTERVAL_MS, PERIODIC_SAVE_INTERVAL_MS);
        }

        /// <summary>
        /// Update a setting value in memory (will be saved on next periodic check)
        /// </summary>
        public void QueueSettingChange(string monitorId, string property, object value)
        {
            try
            {
                var intValue = Convert.ToInt32(value);
                
                // Get or create monitor settings
                var settings = _pendingSettings.GetOrAdd(monitorId, _ => new MonitorSettings());
                
                // Update the specific property
                switch (property)
                {
                    case "Brightness":
                        settings.Brightness = intValue;
                        break;
                    case "ColorTemperature":
                        settings.ColorTemperature = intValue;
                        break;
                    case "Contrast":
                        settings.Contrast = intValue;
                        break;
                    case "Volume":
                        settings.Volume = intValue;
                        break;
                    default:
                        Logger.LogWarning($"Unknown property: {property}");
                        return;
                }
                
                settings.IsDirty = true;
                _hasPendingChanges = true;
                
                Logger.LogTrace($"[Queue] Updated {property}={intValue} for monitor '{monitorId}' in memory");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to queue setting change: {ex.Message}");
            }
        }

        /// <summary>
        /// Periodic check (every 1 second) - save if there are pending changes
        /// </summary>
        private async void CheckAndSavePendingChanges(object? state)
        {
            // Quick check without lock - if no changes, skip
            if (_disposed || !_hasPendingChanges)
            {
                return;
            }

            await _fileAccessSemaphore.WaitAsync();
            try
            {
                // Double check after acquiring lock
                if (!_hasPendingChanges)
                {
                    return;
                }

                // Load current settings
                var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(MODULE_NAME);
                
                if (settings.Properties.SavedMonitorSettings == null)
                {
                    settings.Properties.SavedMonitorSettings = new Dictionary<string, MonitorSavedSettings>();
                }

                int changeCount = 0;
                var now = DateTime.Now;

                // Apply all dirty monitor settings
                foreach (var kvp in _pendingSettings)
                {
                    var monitorId = kvp.Key;
                    var monitorSettings = kvp.Value;

                    if (!monitorSettings.IsDirty)
                    {
                        continue;
                    }

                    // Get or create saved settings for this monitor
                    if (!settings.Properties.SavedMonitorSettings.ContainsKey(monitorId))
                    {
                        settings.Properties.SavedMonitorSettings[monitorId] = new MonitorSavedSettings();
                    }

                    var savedSettings = settings.Properties.SavedMonitorSettings[monitorId];
                    
                    // Update all properties
                    savedSettings.Brightness = monitorSettings.Brightness;
                    savedSettings.ColorTemperature = monitorSettings.ColorTemperature;
                    savedSettings.Contrast = monitorSettings.Contrast;
                    savedSettings.Volume = monitorSettings.Volume;
                    savedSettings.LastUpdated = now;

                    // Mark as clean
                    monitorSettings.IsDirty = false;
                    changeCount++;
                }

                // If we saved anything, write to file
                if (changeCount > 0)
                {
                    _settingsUtils.SaveSettings(settings.ToJsonString(), MODULE_NAME);
                    Logger.LogInfo($"[Periodic Save] Saved settings for {changeCount} monitor(s)");
                }

                // Reset flag
                _hasPendingChanges = false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save pending changes: {ex.Message}");
            }
            finally
            {
                _fileAccessSemaphore.Release();
            }
        }

        /// <summary>
        /// Force immediate save of all pending changes
        /// </summary>
        public async Task FlushPendingChangesAsync()
        {
            // Temporarily stop the timer to prevent concurrent execution
            _periodicSaveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            try
            {
                // Trigger immediate save
                CheckAndSavePendingChanges(null);
                
                // Give it a moment to complete
                await Task.Delay(100);
            }
            finally
            {
                // Restart the periodic timer
                _periodicSaveTimer.Change(PERIODIC_SAVE_INTERVAL_MS, PERIODIC_SAVE_INTERVAL_MS);
            }
        }

        /// <summary>
        /// Thread-safe method to save monitor info
        /// </summary>
        public async Task SaveMonitorInfoAsync(IReadOnlyList<PowerDisplay.Core.Models.Monitor> monitors)
        {
            await _fileAccessSemaphore.WaitAsync();
            try
            {
                var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(MODULE_NAME);
                
                var monitorInfoList = monitors.Select(monitor =>
                {
                    var existingMonitor = settings.Properties.Monitors.FirstOrDefault(m =>
                        m.HardwareId == monitor.HardwareId || m.InternalName == GetInternalName(monitor));

                    var monitorInfo = new MonitorInfo(
                        monitor.Name,
                        GetInternalName(monitor),
                        monitor.HardwareId,
                        GetCommunicationMethod(monitor),
                        GetMonitorType(monitor),
                        monitor.CurrentBrightness,
                        monitor.CurrentColorTemperature
                    );

                    if (existingMonitor != null)
                    {
                        monitorInfo.EnableColorTemperature = existingMonitor.EnableColorTemperature;
                        monitorInfo.EnableContrast = existingMonitor.EnableContrast;
                        monitorInfo.EnableVolume = existingMonitor.EnableVolume;
                        monitorInfo.IsHidden = existingMonitor.IsHidden;
                    }

                    return monitorInfo;
                }).ToList();

                settings.Properties.Monitors = monitorInfoList;
                _settingsUtils.SaveSettings(settings.ToJsonString(), MODULE_NAME);
                
                Logger.LogInfo($"Synchronized save of monitor info for {monitors.Count} monitors");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save monitor info: {ex.Message}");
            }
            finally
            {
                _fileAccessSemaphore.Release();
            }
        }

        private string GetInternalName(PowerDisplay.Core.Models.Monitor monitor)
        {
            if (monitor.Type == PowerDisplay.Core.Models.MonitorType.Internal)
            {
                return "Internal Display";
            }

            var id = monitor.Id;
            if (id.StartsWith("DDC_"))
            {
                return id.Substring(4);
            }

            return id;
        }

        private string GetCommunicationMethod(PowerDisplay.Core.Models.Monitor monitor)
        {
            if (!string.IsNullOrEmpty(monitor.CommunicationMethod))
            {
                return monitor.CommunicationMethod;
            }

            switch (monitor.Type)
            {
                case PowerDisplay.Core.Models.MonitorType.External:
                    return "DDC/CI";
                case PowerDisplay.Core.Models.MonitorType.Internal:
                    return "WMI";
                default:
                    return "Unknown";
            }
        }

        private string GetMonitorType(PowerDisplay.Core.Models.Monitor monitor)
        {
            switch (monitor.Type)
            {
                case PowerDisplay.Core.Models.MonitorType.External:
                    return "External Monitor";
                case PowerDisplay.Core.Models.MonitorType.Internal:
                    return "Internal Monitor";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Periodically save monitor information to settings file
        /// </summary>
        public async Task SaveMonitorInfoPeriodicallyAsync(IReadOnlyList<PowerDisplay.Core.Models.Monitor> monitors, CancellationToken cancellationToken)
        {
            try
            {
                // Wait for a while to ensure the application is fully initialized
                await Task.Delay(2000, cancellationToken);

                // First update to settings file
                await SaveMonitorInfoAsync(monitors);

                // Periodically update settings file (every 60 seconds to reduce file conflicts)
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(60000, cancellationToken); // 60 seconds
                    await SaveMonitorInfoAsync(monitors);
                    
                    Logger.LogInfo("Periodic monitor info update completed");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in periodic monitor info update: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                // Stop the periodic timer
                _periodicSaveTimer?.Dispose();
                
                // Flush any remaining changes
                if (_hasPendingChanges)
                {
                    CheckAndSavePendingChanges(null);
                }
                
                _fileAccessSemaphore?.Dispose();
            }
        }
    }
}
