// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Manages monitor parameter state in a separate file from main settings.
    /// This avoids FileSystemWatcher feedback loops by separating read-only config (settings.json)
    /// from frequently-updated state (monitor_state.json).
    /// </summary>
    public class MonitorStateManager : IDisposable
    {
        private const int SaveIntervalMs = 2000; // Check every 2 seconds
        
        private readonly Timer _saveTimer;
        private readonly string _stateFilePath;
        private readonly ConcurrentDictionary<string, MonitorParameters> _parameters = new();
        
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        
        private volatile bool _isDirty = false;
        private bool _disposed;
        
        // Simple mutual exclusion for save operations (0 = idle, 1 = saving)
        private int _isSaving = 0;

        /// <summary>
        /// Monitor parameters with thread-safe volatile properties
        /// </summary>
        private sealed class MonitorParameters
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

        /// <summary>
        /// Serializable state for JSON persistence
        /// Dictionary key should be HardwareId (e.g., "GSM5C6D") for stable identification
        /// Legacy files may have used InternalName (e.g., "DISPLAY1_0_3") which will still load but won't match after reconnection
        /// </summary>
        private sealed class MonitorStateFile
        {
            public Dictionary<string, MonitorStateEntry> Monitors { get; set; } = new();
            public DateTime LastUpdated { get; set; }
        }

        private sealed class MonitorStateEntry
        {
            public int Brightness { get; set; }
            public int ColorTemperature { get; set; }
            public int Contrast { get; set; }
            public int Volume { get; set; }
            public DateTime LastUpdated { get; set; }
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
            
            _stateFilePath = Path.Combine(powerToysPath, "monitor_state.json");
            
            // Load existing state if available
            LoadStateFromDisk();
            
            // Start periodic timer that checks every 2 seconds
            _saveTimer = new Timer(
                _ => SaveStateIfDirty(),
                null,
                TimeSpan.FromSeconds(SaveIntervalMs / 1000.0),
                TimeSpan.FromSeconds(SaveIntervalMs / 1000.0)
            );

            Logger.LogInfo($"MonitorStateManager initialized, state file: {_stateFilePath}");
        }

        /// <summary>
        /// Update monitor parameter in memory (lock-free, non-blocking)
        /// Uses HardwareId as the stable key
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

                // Get or create parameter entry using HardwareId
                var parameters = _parameters.GetOrAdd(hardwareId, _ => new MonitorParameters());
                
                // Update the specific property (volatile write)
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
                
                // Mark as dirty (will be saved in next timer cycle)
                _isDirty = true;
                
                Logger.LogTrace($"[State] Updated {property}={value} for monitor HardwareId='{hardwareId}'");
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

            if (_parameters.TryGetValue(hardwareId, out var parameters))
            {
                return (parameters.Brightness, parameters.ColorTemperature, parameters.Contrast, parameters.Volume);
            }
            return null;
        }

        /// <summary>
        /// Check if state exists for a monitor (by HardwareId)
        /// </summary>
        public bool HasMonitorState(string hardwareId)
        {
            return !string.IsNullOrEmpty(hardwareId) && _parameters.ContainsKey(hardwareId);
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
                var state = JsonSerializer.Deserialize<MonitorStateFile>(json);

                if (state?.Monitors != null)
                {
                    foreach (var kvp in state.Monitors)
                    {
                        var monitorKey = kvp.Key; // Should be HardwareId (e.g., "GSM5C6D")
                        var entry = kvp.Value;
                        
                        var parameters = _parameters.GetOrAdd(monitorKey, _ => new MonitorParameters());
                        parameters.Brightness = entry.Brightness;
                        parameters.ColorTemperature = entry.ColorTemperature;
                        parameters.Contrast = entry.Contrast;
                        parameters.Volume = entry.Volume;
                    }

                    Logger.LogInfo($"[State] Loaded state for {state.Monitors.Count} monitors from {_stateFilePath}");
                    Logger.LogInfo($"[State] Monitor keys in state file: {string.Join(", ", state.Monitors.Keys)}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load monitor state: {ex.Message}");
            }
        }

        /// <summary>
        /// Periodic save check - saves state if dirty
        /// </summary>
        private async void SaveStateIfDirty()
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

                // Build state file
                var state = new MonitorStateFile
                {
                    LastUpdated = DateTime.Now
                };

                var now = DateTime.Now;
                foreach (var kvp in _parameters)
                {
                    var monitorId = kvp.Key;
                    var parameters = kvp.Value;
                    
                    state.Monitors[monitorId] = new MonitorStateEntry
                    {
                        Brightness = parameters.Brightness,  // Volatile read
                        ColorTemperature = parameters.ColorTemperature,
                        Contrast = parameters.Contrast,
                        Volume = parameters.Volume,
                        LastUpdated = now
                    };
                }

                // Write to disk
                var json = JsonSerializer.Serialize(state, _jsonOptions);
                await File.WriteAllTextAsync(_stateFilePath, json);
                
                // Clear dirty flag
                _isDirty = false;
                
                Logger.LogInfo($"[State] Saved state for {state.Monitors.Count} monitors");
                
                // Release save permission
                Interlocked.Exchange(ref _isSaving, 0);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save monitor state: {ex.Message}");
                // Release save permission even on error
                Interlocked.Exchange(ref _isSaving, 0);
            }
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
                SaveStateIfDirty();
                
                // Wait for save to complete
                while (Volatile.Read(ref _isSaving) == 1)
                {
                    await Task.Delay(50);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            
            // Stop timer
            _saveTimer?.Dispose();
            
            // Flush any pending changes
            FlushAsync().GetAwaiter().GetResult();
            
            Logger.LogInfo("MonitorStateManager disposed");
        }
    }
}
