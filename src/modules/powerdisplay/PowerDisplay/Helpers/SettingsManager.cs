// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Settings manager with lock-free snapshot-based mechanism
    /// </summary>
    public class SettingsManager : IDisposable
    {
        private readonly ISettingsUtils _settingsUtils;
        private readonly Timer _saveTimer;
        private const string MODULE_NAME = "PowerDisplay";
        private const int SAVE_INTERVAL_MS = 2000; // Check every 2 seconds
        private bool _disposed;

        // Core: Single memory snapshot (volatile for thread-safe reference updates)
        private volatile SettingsSnapshot _currentSnapshot = new();
        private volatile bool _isDirty = false;
        
        // Simple mutual exclusion for save operations (0 = idle, 1 = saving)
        private int _isSaving = 0;

        /// <summary>
        /// Immutable snapshot containing all settings to be saved
        /// </summary>
        private class SettingsSnapshot
        {
            // Monitor list info (changes infrequently)
            public ImmutableList<MonitorInfo> MonitorList { get; init; } = ImmutableList<MonitorInfo>.Empty;
            
            // Monitor parameter values (changes frequently)
            public ConcurrentDictionary<string, MonitorParameters> Parameters { get; init; } = new();
        }

        /// <summary>
        /// Monitor parameters with thread-safe volatile properties
        /// </summary>
        private class MonitorParameters
        {
            private int _brightness;
            private int _colorTemperature;
            private int _contrast;
            private int _volume;

            public int Brightness
            {
                get => Volatile.Read(ref _brightness);
                set => Volatile.Write(ref _brightness, value);
            }

            public int ColorTemperature
            {
                get => Volatile.Read(ref _colorTemperature);
                set => Volatile.Write(ref _colorTemperature, value);
            }

            public int Contrast
            {
                get => Volatile.Read(ref _contrast);
                set => Volatile.Write(ref _contrast, value);
            }

            public int Volume
            {
                get => Volatile.Read(ref _volume);
                set => Volatile.Write(ref _volume, value);
            }
        }

        public SettingsManager(ISettingsUtils settingsUtils)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            
            // Start periodic timer that checks every 2 seconds
            _saveTimer = new Timer(
                _ => SaveSnapshotIfDirty(),
                null,
                TimeSpan.FromSeconds(SAVE_INTERVAL_MS / 1000.0),
                TimeSpan.FromSeconds(SAVE_INTERVAL_MS / 1000.0)
            );
        }

        /// <summary>
        /// Update monitor parameter in memory snapshot (lock-free, non-blocking)
        /// </summary>
        public void UpdateMonitorParameter(string monitorId, string property, int value)
        {
            try
            {
                // Get or create monitor parameters (thread-safe)
                var parameters = _currentSnapshot.Parameters.GetOrAdd(
                    monitorId,
                    _ => new MonitorParameters()
                );
                
                // Update the specific property (using Volatile write)
                switch (property)
                {
                    case "Brightness":
                        parameters.Brightness = value;
                        break;
                    case "ColorTemperature":
                        parameters.ColorTemperature = value;
                        break;
                    case "Contrast":
                        parameters.Contrast = value;
                        break;
                    case "Volume":
                        parameters.Volume = value;
                        break;
                    default:
                        Logger.LogWarning($"Unknown property: {property}");
                        return;
                }
                
                // Mark as dirty
                _isDirty = true;
                
                Logger.LogTrace($"[Update] {property}={value} for monitor '{monitorId}'");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// Update monitor list in memory snapshot (lock-free, non-blocking)
        /// </summary>
        public void UpdateMonitorList(IReadOnlyList<PowerDisplay.Core.Models.Monitor> monitors)
        {
            try
            {
                var newList = monitors.Select(m => CreateMonitorInfo(m)).ToImmutableList();
                
                // Get current snapshot
                var currentSnapshot = _currentSnapshot;
                
                // Check if monitor list really changed
                if (!MonitorListChanged(currentSnapshot.MonitorList, newList))
                {
                    Logger.LogTrace($"[Update] Monitor list unchanged, skipping");
                    return;
                }
                
                // Create new snapshot (reuse existing Parameters)
                var newSnapshot = new SettingsSnapshot
                {
                    MonitorList = newList,
                    Parameters = currentSnapshot.Parameters  // Reuse
                };
                
                // Atomically replace entire snapshot
                _currentSnapshot = newSnapshot;
                _isDirty = true;
                
                Logger.LogInfo($"[Update] Monitor list updated ({newList.Count} monitors)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update monitor list: {ex.Message}");
            }
        }

        /// <summary>
        /// Periodic save check - saves snapshot if dirty (single save point)
        /// </summary>
        private async void SaveSnapshotIfDirty()
        {
            try
            {
                // Quick check without lock
                if (!_isDirty || _disposed)
                {
                    return;
                }

                // Try to acquire save permission (non-blocking)
                if (Interlocked.CompareExchange(ref _isSaving, 1, 0) != 0)
                {
                    // Already saving, skip this cycle
                    return;
                }

                // Double check
                if (!_isDirty)
                {
                    Interlocked.Exchange(ref _isSaving, 0);
                    return;
                }

                // Atomically read snapshot reference (stable view, won't change during read)
                var snapshot = _currentSnapshot;
                
                // Read current settings from file
                var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(MODULE_NAME);
                
                // Update monitor list
                settings.Properties.Monitors = snapshot.MonitorList.ToList();
                
                // Update monitor parameters
                if (settings.Properties.SavedMonitorSettings == null)
                {
                    settings.Properties.SavedMonitorSettings = new Dictionary<string, MonitorSavedSettings>();
                }

                var now = DateTime.Now;
                foreach (var kvp in snapshot.Parameters)
                {
                    var monitorId = kvp.Key;
                    var parameters = kvp.Value;
                    
                    if (!settings.Properties.SavedMonitorSettings.TryGetValue(monitorId, out var saved))
                    {
                        saved = new MonitorSavedSettings();
                        settings.Properties.SavedMonitorSettings[monitorId] = saved;
                    }
                    
                    saved.Brightness = parameters.Brightness;  // Volatile read
                    saved.ColorTemperature = parameters.ColorTemperature;
                    saved.Contrast = parameters.Contrast;
                    saved.Volume = parameters.Volume;
                    saved.LastUpdated = now;
                }
                
                // Single file write
                _settingsUtils.SaveSettings(settings.ToJsonString(), MODULE_NAME);
                
                // Clear dirty flag
                _isDirty = false;
                
                Logger.LogInfo($"[Save] Saved snapshot (monitors: {snapshot.MonitorList.Count}, parameters: {snapshot.Parameters.Count})");
                
                // Release save permission
                Interlocked.Exchange(ref _isSaving, 0);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save snapshot: {ex.Message}");
                // Release save permission even on error
                Interlocked.Exchange(ref _isSaving, 0);
            }
            catch
            {
                // Ensure we never crash on save
                Interlocked.Exchange(ref _isSaving, 0);
            }

            await Task.CompletedTask;  // Suppress async warning
        }

        /// <summary>
        /// Flush pending changes immediately (for program exit)
        /// </summary>
        public async Task FlushAsync()
        {
            if (_isDirty && !_disposed)
            {
                // Wait for any ongoing save to complete
                while (Volatile.Read(ref _isSaving) == 1)
                {
                    await Task.Delay(50);
                }
                
                // Trigger save
                SaveSnapshotIfDirty();
                
                // Wait for save to complete
                while (Volatile.Read(ref _isSaving) == 1)
                {
                    await Task.Delay(50);
                }
            }
        }

        /// <summary>
        /// Compare monitor lists to detect changes
        /// </summary>
        private static bool MonitorListChanged(ImmutableList<MonitorInfo> oldList, ImmutableList<MonitorInfo> newList)
        {
            if (oldList.Count != newList.Count)
            {
                return true;
            }
            
            // Compare key fields
            for (int i = 0; i < oldList.Count; i++)
            {
                if (oldList[i].HardwareId != newList[i].HardwareId ||
                    oldList[i].Name != newList[i].Name)
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Create MonitorInfo from Monitor model
        /// </summary>
        private MonitorInfo CreateMonitorInfo(PowerDisplay.Core.Models.Monitor monitor)
        {
            // Read current settings to preserve existing configuration
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(MODULE_NAME);
            
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

            // Preserve existing configuration if available
            if (existingMonitor != null)
            {
                monitorInfo.EnableColorTemperature = existingMonitor.EnableColorTemperature;
                monitorInfo.EnableContrast = existingMonitor.EnableContrast;
                monitorInfo.EnableVolume = existingMonitor.EnableVolume;
                monitorInfo.IsHidden = existingMonitor.IsHidden;
            }

            return monitorInfo;
        }

        private static string GetInternalName(PowerDisplay.Core.Models.Monitor monitor)
        {
            if (monitor.Type == PowerDisplay.Core.Models.MonitorType.Internal)
            {
                return "Internal Display";
            }

            var id = monitor.Id;
            if (id.StartsWith("DDC_", StringComparison.Ordinal))
            {
                return id.Substring(4);
            }

            return id;
        }

        private static string GetCommunicationMethod(PowerDisplay.Core.Models.Monitor monitor)
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

        private static string GetMonitorType(PowerDisplay.Core.Models.Monitor monitor)
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                // Stop the timer
                _saveTimer?.Dispose();
                
                // Synchronous save of remaining changes
                if (_isDirty)
                {
                    SaveSnapshotIfDirty();
                    
                    // Simple wait for save to complete
                    int retries = 20;  // Max 1 second wait
                    while (Volatile.Read(ref _isSaving) == 1 && retries-- > 0)
                    {
                        Thread.Sleep(50);
                    }
                }
            }
        }
    }
}
