// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Serialization;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Manages monitor parameter state in a separate file from main settings.
    /// This avoids FileSystemWatcher feedback loops by separating read-only config (settings.json)
    /// from frequently updated state (monitor_state.json).
    /// Simplified to use direct save strategy for reliability and simplicity (KISS principle).
    /// </summary>
    public partial class MonitorStateManager : IDisposable
    {
        private readonly string _stateFilePath;
        private readonly ConcurrentDictionary<string, MonitorState> _states = new();
        private readonly object _statesLock = new();
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

            public int ColorTemperatureVcp { get; set; }

            public int Contrast { get; set; }

            public int Volume { get; set; }

            public string? CapabilitiesRaw { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorStateManager"/> class.
        /// Uses PathConstants for consistent path management.
        /// </summary>
        public MonitorStateManager()
        {
            // Use PathConstants for consistent path management
            PathConstants.EnsurePowerDisplayFolderExists();
            _stateFilePath = PathConstants.MonitorStateFilePath;

            // Initialize debouncer for batching rapid updates (e.g., slider drag)
            _saveDebouncer = new SimpleDebouncer(SaveDebounceMs);

            // Load existing state if available
            LoadStateFromDisk();

            Logger.LogInfo($"MonitorStateManager initialized with debounced-save strategy (debounce: {SaveDebounceMs}ms), state file: {_stateFilePath}");
        }

        /// <summary>
        /// Update monitor parameter and schedule debounced save to disk.
        /// Uses Monitor.Id as the stable key (e.g., "DDC_GSM5C6D_1", "WMI_BOE0900_2").
        /// Debounced-save strategy reduces disk I/O by batching rapid updates (e.g., during slider drag).
        /// </summary>
        /// <param name="monitorId">The monitor's unique Id (e.g., "DDC_GSM5C6D_1").</param>
        /// <param name="property">The property name to update (Brightness, ColorTemperature, Contrast, or Volume).</param>
        /// <param name="value">The new value.</param>
        public void UpdateMonitorParameter(string monitorId, string property, int value)
        {
            try
            {
                if (string.IsNullOrEmpty(monitorId))
                {
                    Logger.LogWarning($"Cannot update monitor parameter: monitorId is empty");
                    return;
                }

                var state = _states.GetOrAdd(monitorId, _ => new MonitorState());

                // Update the specific property
                bool shouldSave = true;
                switch (property)
                {
                    case "Brightness":
                        state.Brightness = value;
                        break;
                    case "ColorTemperature":
                        state.ColorTemperatureVcp = value;
                        break;
                    case "Contrast":
                        state.Contrast = value;
                        break;
                    case "Volume":
                        state.Volume = value;
                        break;
                    default:
                        Logger.LogWarning($"Unknown property: {property}");
                        shouldSave = false;
                        break;
                }

                if (shouldSave)
                {
                    // Mark dirty for flush on dispose
                    _isDirty = true;
                }

                // Schedule debounced save (SimpleDebouncer handles cancellation of previous calls)
                if (shouldSave)
                {
                    _saveDebouncer.Debounce(SaveStateToDiskAsync);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update monitor parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// Get saved parameters for a monitor using Monitor.Id.
        /// </summary>
        /// <param name="monitorId">The monitor's unique Id (e.g., "DDC_GSM5C6D_1").</param>
        /// <returns>A tuple of (Brightness, ColorTemperatureVcp, Contrast, Volume) or null if not found.</returns>
        public (int Brightness, int ColorTemperatureVcp, int Contrast, int Volume)? GetMonitorParameters(string monitorId)
        {
            if (string.IsNullOrEmpty(monitorId))
            {
                return null;
            }

            if (_states.TryGetValue(monitorId, out var state))
            {
                return (state.Brightness, state.ColorTemperatureVcp, state.Contrast, state.Volume);
            }

            return null;
        }

        /// <summary>
        /// Load state from disk.
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
                var stateFile = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.MonitorStateFile);

                if (stateFile?.Monitors != null)
                {
                    foreach (var kvp in stateFile.Monitors)
                    {
                        var monitorKey = kvp.Key; // Should be HardwareId (e.g., "GSM5C6D")
                        var entry = kvp.Value;

                        _states[monitorKey] = new MonitorState
                        {
                            Brightness = entry.Brightness,
                            ColorTemperatureVcp = entry.ColorTemperatureVcp,
                            Contrast = entry.Contrast,
                            Volume = entry.Volume,
                            CapabilitiesRaw = entry.CapabilitiesRaw,
                        };
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
        /// Called by timer after debounce period.
        /// </summary>
        private async Task SaveStateToDiskAsync()
        {
            try
            {
                if (_disposed)
                {
                    return;
                }

                var (json, monitorCount) = BuildStateJson();

                // Write to disk asynchronously
                await File.WriteAllTextAsync(_stateFilePath, json);

                // Clear dirty flag after successful save
                _isDirty = false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save monitor state: {ex.Message}");
            }
        }

        /// <summary>
        /// Save current state to disk synchronously.
        /// Called during Dispose to flush pending changes without risk of deadlock.
        /// </summary>
        private void SaveStateToDiskSync()
        {
            try
            {
                var (json, monitorCount) = BuildStateJson();

                // Write to disk synchronously - safe for Dispose
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save monitor state (sync): {ex.Message}");
            }
        }

        /// <summary>
        /// Build the JSON string for state file.
        /// Shared logic between async and sync save methods.
        /// </summary>
        /// <returns>Tuple of (JSON string, monitor count)</returns>
        private (string Json, int MonitorCount) BuildStateJson()
        {
            var now = DateTime.Now;
            var stateFile = new MonitorStateFile
            {
                LastUpdated = now,
            };

            foreach (var kvp in _states)
            {
                var monitorId = kvp.Key;
                var state = kvp.Value;

                stateFile.Monitors[monitorId] = new MonitorStateEntry
                {
                    Brightness = state.Brightness,
                    ColorTemperatureVcp = state.ColorTemperatureVcp,
                    Contrast = state.Contrast,
                    Volume = state.Volume,
                    CapabilitiesRaw = state.CapabilitiesRaw,
                    LastUpdated = now,
                };
            }

            var json = JsonSerializer.Serialize(stateFile, ProfileSerializationContext.Default.MonitorStateFile);
            return (json, stateFile.Monitors.Count);
        }

        /// <summary>
        /// Disposes the MonitorStateManager, flushing any pending state changes.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            bool wasDirty = _isDirty;
            _disposed = true;
            _isDirty = false;

            // Dispose debouncer first to cancel any pending saves
            _saveDebouncer?.Dispose();

            // Flush any pending changes before disposing using sync method to avoid deadlock
            if (wasDirty)
            {
                Logger.LogInfo("Flushing pending state changes before dispose");
                SaveStateToDiskSync();
            }

            Logger.LogInfo("MonitorStateManager disposed");
            GC.SuppressFinalize(this);
        }
    }
}
