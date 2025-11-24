// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common;
using PowerDisplay.Common.Utils;
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
        private readonly SimpleDebouncer _saveDebouncer;

        private bool _disposed;
        private bool _isDirty; // Track pending changes for flush on dispose
        private const int SaveDebounceMs = 2000; // Save 2 seconds after last update

        /// <summary>
        /// Monitor state data (internal tracking, not serialized)
        /// </summary>
        private sealed class MonitorState
        {
            public int Brightness { get; set; }

            public int ColorTemperature { get; set; }

            public int Contrast { get; set; }

            public int Volume { get; set; }

            public string? CapabilitiesRaw { get; set; }
        }

        public MonitorStateManager()
        {
            // Use PathConstants for consistent path management
            PathConstants.EnsurePowerDisplayFolderExists();
            _stateFilePath = Path.Combine(PathConstants.PowerDisplayFolderPath, AppConstants.State.StateFileName);

            // Initialize debouncer for batching rapid updates (e.g., slider drag)
            _saveDebouncer = new SimpleDebouncer(SaveDebounceMs);

            // Load existing state if available
            LoadStateFromDisk();

            Logger.LogInfo($"MonitorStateManager initialized with debounced-save strategy (debounce: {SaveDebounceMs}ms), state file: {_stateFilePath}");
        }

        /// <summary>
        /// Update monitor parameter and schedule debounced save to disk.
        /// Uses HardwareId as the stable key.
        /// Debounced-save strategy reduces disk I/O by batching rapid updates (e.g., during slider drag).
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

                    // Mark dirty for flush on dispose
                    _isDirty = true;
                }

                // Schedule debounced save (SimpleDebouncer handles cancellation of previous calls)
                _saveDebouncer.Debounce(SaveStateToDiskAsync);
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
                                CapabilitiesRaw = entry.CapabilitiesRaw,
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
        /// Save current state to disk immediately (async).
        /// Called by timer after debounce period or on dispose to flush pending changes.
        /// </summary>
        private async Task SaveStateToDiskAsync()
        {
            try
            {
                if (_disposed)
                {
                    return;
                }

                // Build state file
                var now = DateTime.Now;
                var stateFile = new MonitorStateFile
                {
                    LastUpdated = now,
                };

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
                            CapabilitiesRaw = state.CapabilitiesRaw,
                            LastUpdated = now,
                        };
                    }
                }

                // Write to disk asynchronously
                var json = JsonSerializer.Serialize(stateFile, AppJsonContext.Default.MonitorStateFile);
                await File.WriteAllTextAsync(_stateFilePath, json);

                // Clear dirty flag after successful save
                lock (_lock)
                {
                    _isDirty = false;
                }

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

            bool wasDirty;
            lock (_lock)
            {
                wasDirty = _isDirty;
                _disposed = true;
                _isDirty = false;
            }

            // Dispose debouncer first to cancel any pending saves
            _saveDebouncer?.Dispose();

            // Flush any pending changes before disposing
            if (wasDirty)
            {
                Logger.LogInfo("Flushing pending state changes before dispose");
                SaveStateToDiskAsync().GetAwaiter().GetResult();
            }

            Logger.LogInfo("MonitorStateManager disposed");
            GC.SuppressFinalize(this);
        }
    }
}
