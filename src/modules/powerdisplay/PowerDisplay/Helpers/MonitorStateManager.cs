// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Configuration;
using PowerDisplay.Serialization;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Manages monitor parameter state in a separate file from main settings.
    /// This avoids FileSystemWatcher feedback loops by separating read-only config (settings.json)
    /// from frequently-updated state (monitor_state.json).
    /// Simplified to use direct save strategy for reliability and simplicity (KISS principle).
    /// </summary>
    public partial class MonitorStateManager : IDisposable
    {
        private readonly string _stateFilePath;
        private readonly Dictionary<string, MonitorState> _states = new();
        private readonly object _lock = new object();

        private bool _disposed;

        /// <summary>
        /// Monitor state data (internal tracking, not serialized)
        /// </summary>
        private sealed class MonitorState
        {
            public int Brightness { get; set; }

            public int ColorTemperature { get; set; }

            public int Contrast { get; set; }

            public int Volume { get; set; }
        }

        public MonitorStateManager()
        {
            // Store state file in same location as settings.json but with different name
            var settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var powerToysPath = Path.Combine(settingsPath, "Microsoft", "PowerToys", "PowerDisplay");

            if (!Directory.Exists(powerToysPath))
            {
                Directory.CreateDirectory(powerToysPath);
            }

            _stateFilePath = Path.Combine(powerToysPath, AppConstants.State.StateFileName);

            // Load existing state if available
            LoadStateFromDisk();

            Logger.LogInfo($"MonitorStateManager initialized with direct-save strategy, state file: {_stateFilePath}");
        }

        /// <summary>
        /// Update monitor parameter and save immediately to disk.
        /// Uses HardwareId as the stable key.
        /// Direct-save strategy ensures no data loss and simplifies code (KISS principle).
        /// </summary>
        public void UpdateMonitorParameter(string hardwareId, string property, int value)
        {
            try
            {
                if (string.IsNullOrEmpty(hardwareId))
                {
                    Logger.LogWarning($"Cannot update monitor parameter: HardwareId is empty");
                    return;
                }

                lock (_lock)
                {
                    // Get or create state entry using HardwareId
                    if (!_states.TryGetValue(hardwareId, out var state))
                    {
                        state = new MonitorState();
                        _states[hardwareId] = state;
                    }

                    // Update the specific property
                    switch (property)
                    {
                        case "Brightness":
                            state.Brightness = value;
                            break;
                        case "ColorTemperature":
                            state.ColorTemperature = value;
                            break;
                        case "Contrast":
                            state.Contrast = value;
                            break;
                        case "Volume":
                            state.Volume = value;
                            break;
                        default:
                            Logger.LogWarning($"Unknown property: {property}");
                            return;
                    }
                }

                // Save immediately after update - simple and reliable!
                SaveStateToDisk();

                Logger.LogTrace($"[State] Updated and saved {property}={value} for monitor HardwareId='{hardwareId}'");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update monitor parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// Get saved parameters for a monitor using HardwareId
        /// </summary>
        public (int Brightness, int ColorTemperature, int Contrast, int Volume)? GetMonitorParameters(string hardwareId)
        {
            if (string.IsNullOrEmpty(hardwareId))
            {
                return null;
            }

            lock (_lock)
            {
                if (_states.TryGetValue(hardwareId, out var state))
                {
                    return (state.Brightness, state.ColorTemperature, state.Contrast, state.Volume);
                }
            }

            return null;
        }

        /// <summary>
        /// Check if state exists for a monitor (by HardwareId)
        /// </summary>
        public bool HasMonitorState(string hardwareId)
        {
            if (string.IsNullOrEmpty(hardwareId))
            {
                return false;
            }

            lock (_lock)
            {
                return _states.ContainsKey(hardwareId);
            }
        }

        /// <summary>
        /// Load state from disk
        /// </summary>
        private void LoadStateFromDisk()
        {
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    Logger.LogInfo("[State] No existing state file found, starting fresh");
                    return;
                }

                var json = File.ReadAllText(_stateFilePath);
                var stateFile = JsonSerializer.Deserialize(json, AppJsonContext.Default.MonitorStateFile);

                if (stateFile?.Monitors != null)
                {
                    lock (_lock)
                    {
                        foreach (var kvp in stateFile.Monitors)
                        {
                            var monitorKey = kvp.Key; // Should be HardwareId (e.g., "GSM5C6D")
                            var entry = kvp.Value;

                            _states[monitorKey] = new MonitorState
                            {
                                Brightness = entry.Brightness,
                                ColorTemperature = entry.ColorTemperature,
                                Contrast = entry.Contrast,
                                Volume = entry.Volume,
                            };
                        }
                    }

                    Logger.LogInfo($"[State] Loaded state for {stateFile.Monitors.Count} monitors from {_stateFilePath}");
                    Logger.LogInfo($"[State] Monitor keys in state file: {string.Join(", ", stateFile.Monitors.Keys)}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load monitor state: {ex.Message}");
            }
        }

        /// <summary>
        /// Save current state to disk immediately.
        /// Simplified direct-save approach - no timer, no dirty flags, just save!
        /// </summary>
        private void SaveStateToDisk()
        {
            try
            {
                if (_disposed)
                {
                    return;
                }

                // Build state file
                var stateFile = new MonitorStateFile
                {
                    LastUpdated = DateTime.Now,
                };

                var now = DateTime.Now;

                lock (_lock)
                {
                    foreach (var kvp in _states)
                    {
                        var monitorId = kvp.Key;
                        var state = kvp.Value;

                        stateFile.Monitors[monitorId] = new MonitorStateEntry
                        {
                            Brightness = state.Brightness,
                            ColorTemperature = state.ColorTemperature,
                            Contrast = state.Contrast,
                            Volume = state.Volume,
                            LastUpdated = now,
                        };
                    }
                }

                // Write to disk
                var json = JsonSerializer.Serialize(stateFile, AppJsonContext.Default.MonitorStateFile);
                File.WriteAllText(_stateFilePath, json);

                Logger.LogDebug($"[State] Saved state for {stateFile.Monitors.Count} monitors");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save monitor state: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                _disposed = true;
            }

            // State is already saved with each update, no need for final flush!
            Logger.LogInfo("MonitorStateManager disposed");
            GC.SuppressFinalize(this);
        }
    }
}
