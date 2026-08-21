// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Serialization;
using PowerDisplay.Common.Utils;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Manages monitor parameter state in a separate file from main settings.
    /// This avoids FileSystemWatcher feedback loops by separating read-only config (settings.json)
    /// from frequently updated state (monitor_state.json).
    /// Simplified to use direct save strategy for reliability and simplicity (KISS principle).
    /// </summary>
    public partial class MonitorStateManager : IDisposable, IKnownGoodVcpStore
    {
        private readonly string _stateFilePath;
        private readonly ConcurrentDictionary<string, MonitorState> _states = new(MonitorIdComparer.Instance);
        private readonly SimpleDebouncer _saveDebouncer;
        private readonly object _writeLock = new();

        private volatile bool _disposed;
        private volatile bool _isDirty; // Track pending changes for flush on dispose
        private const int SaveDebounceMs = 2000; // Save 2 seconds after last update

        /// <summary>
        /// Monitor state data (internal tracking, not serialized)
        /// </summary>
        private sealed class MonitorState
        {
            public int? Brightness { get; set; }

            public int? ColorTemperatureVcp { get; set; }

            public int? Contrast { get; set; }

            public int? Volume { get; set; }

            public Dictionary<byte, KnownGoodVcpFeature> KnownGoodVcpFeatures { get; } = new();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorStateManager"/> class.
        /// Uses PathConstants for consistent path management.
        /// </summary>
        public MonitorStateManager()
            : this(PathConstants.MonitorStateFilePath, ensureDefaultDirectory: true)
        {
        }

        internal MonitorStateManager(string stateFilePath)
            : this(stateFilePath, ensureDefaultDirectory: false)
        {
        }

        private MonitorStateManager(string stateFilePath, bool ensureDefaultDirectory)
        {
            if (ensureDefaultDirectory)
            {
                PathConstants.EnsurePowerDisplayFolderExists();
            }

            _stateFilePath = stateFilePath;
            _saveDebouncer = new SimpleDebouncer(SaveDebounceMs);
            LoadStateFromDisk();
        }

        /// <summary>
        /// Update monitor parameter and schedule debounced save to disk.
        /// Uses Monitor.Id as the stable key (new DevicePath-based Id, e.g., <c>\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID1</c>).
        /// Debounced-save strategy reduces disk I/O by batching rapid updates (e.g., during slider drag).
        /// </summary>
        /// <param name="monitorId">The monitor's unique Id (new DevicePath-based format, e.g., <c>\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID1</c>).</param>
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
                lock (state)
                {
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
                }

                if (shouldSave)
                {
                    MarkDirtyAndScheduleSave();
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
        /// <param name="monitorId">The monitor's unique Id (new DevicePath-based format, e.g., <c>\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID1</c>).</param>
        /// <returns>A tuple of (Brightness, ColorTemperatureVcp, Contrast, Volume) or null if not found.</returns>
        public (int? Brightness, int? ColorTemperatureVcp, int? Contrast, int? Volume)? GetMonitorParameters(string monitorId)
        {
            if (string.IsNullOrEmpty(monitorId))
            {
                return null;
            }

            if (_states.TryGetValue(monitorId, out var state))
            {
                lock (state)
                {
                    return (state.Brightness, state.ColorTemperatureVcp, state.Contrast, state.Volume);
                }
            }

            return null;
        }

        public IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId)
        {
            if (string.IsNullOrEmpty(monitorId) || !_states.TryGetValue(monitorId, out var state))
            {
                return new Dictionary<byte, KnownGoodVcpFeature>();
            }

            lock (state)
            {
                return state.KnownGoodVcpFeatures.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone());
            }
        }

        public void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature)
        {
            ArgumentException.ThrowIfNullOrEmpty(monitorId);
            ArgumentNullException.ThrowIfNull(feature);

            // Range validity is not re-checked here: every caller upserts a value it has already
            // proven valid, and the one place untrusted values enter — LoadStateFromDisk — filters
            // them there.
            var state = _states.GetOrAdd(monitorId, _ => new MonitorState());
            lock (state)
            {
                // Re-observing an unchanged value is the common case: every discovery pass re-reads
                // all three continuous codes, and the flyout's Refresh button forces one on demand.
                // Rewriting the whole state file for it would also reset the debouncer, cancelling
                // the pending save with an OperationCanceledException, on every pass.
                if (state.KnownGoodVcpFeatures.TryGetValue(feature.Code, out var existing) &&
                    existing.Current == feature.Current &&
                    existing.Maximum == feature.Maximum)
                {
                    return;
                }

                state.KnownGoodVcpFeatures[feature.Code] = feature.Clone();
            }

            MarkDirtyAndScheduleSave();
        }

        /// <summary>
        /// One-shot upgrade migration: rewrite legacy <c>"{Source}_{EdidId}_{N}"</c> keys
        /// (pre-PR #47712) onto the matching DevicePath-based monitor Ids by joining on
        /// (EdidId, MonitorNumber). Legacy keys are always removed; if no exact match is
        /// found the legacy state is dropped (a warning is logged) rather than risk
        /// attaching to the wrong monitor.
        /// </summary>
        /// <param name="currentlyDiscovered">Ids and Windows DISPLAY numbers of monitors currently discovered.</param>
        public void MigrateLegacyKeys(IEnumerable<(string Id, int MonitorNumber)> currentlyDiscovered)
        {
            if (currentlyDiscovered is null)
            {
                return;
            }

            try
            {
                var discoveredList = currentlyDiscovered as IList<(string Id, int MonitorNumber)> ?? currentlyDiscovered.ToList();
                var legacyKeys = new List<string>();
                foreach (var key in _states.Keys)
                {
                    if (MonitorIdentity.IsLegacyId(key))
                    {
                        legacyKeys.Add(key);
                    }
                }

                if (legacyKeys.Count == 0)
                {
                    return;
                }

                int migrated = 0;
                int dropped = 0;
                foreach (var legacyKey in legacyKeys)
                {
                    var newKey = MonitorIdMigrator.MatchNewId(legacyKey, discoveredList);
                    if (newKey != null && _states.TryGetValue(legacyKey, out var value))
                    {
                        var legacyState = CloneState(value);
                        var canonicalState = _states.GetOrAdd(newKey, legacyState);
                        if (!ReferenceEquals(canonicalState, legacyState))
                        {
                            MergeMissingState(canonicalState, legacyState);
                        }

                        migrated++;
                    }
                    else if (newKey == null)
                    {
                        Logger.LogWarning(
                            $"[MonitorStateManager] Dropping legacy state for '{legacyKey}': no current monitor with matching EdidId+MonitorNumber.");
                        dropped++;
                    }

                    _states.TryRemove(legacyKey, out _);
                }

                MarkDirtyAndScheduleSave();

                Logger.LogInfo(
                    $"[MonitorStateManager] Legacy migration finished: {migrated} migrated, {dropped} dropped (no match).");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MonitorStateManager] Legacy key migration failed: {ex.Message}");
            }
        }

        private static MonitorState CloneState(MonitorState source)
        {
            var clone = new MonitorState();

            lock (source)
            {
                clone.Brightness = source.Brightness;
                clone.ColorTemperatureVcp = source.ColorTemperatureVcp;
                clone.Contrast = source.Contrast;
                clone.Volume = source.Volume;

                foreach (var feature in source.KnownGoodVcpFeatures)
                {
                    clone.KnownGoodVcpFeatures[feature.Key] = feature.Value.Clone();
                }
            }

            return clone;
        }

        private static void MergeMissingState(MonitorState canonical, MonitorState legacy)
        {
            lock (canonical)
            {
                canonical.Brightness ??= legacy.Brightness;
                canonical.ColorTemperatureVcp ??= legacy.ColorTemperatureVcp;
                canonical.Contrast ??= legacy.Contrast;
                canonical.Volume ??= legacy.Volume;

                foreach (var feature in legacy.KnownGoodVcpFeatures)
                {
                    if (!canonical.KnownGoodVcpFeatures.ContainsKey(feature.Key))
                    {
                        canonical.KnownGoodVcpFeatures[feature.Key] = feature.Value.Clone();
                    }
                }
            }
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
                    return;
                }

                var json = File.ReadAllText(_stateFilePath);
                var stateFile = JsonSerializer.Deserialize(json, MonitorStateSerializationContext.Default.MonitorStateFile);

                if (stateFile?.Monitors != null)
                {
                    foreach (var kvp in stateFile.Monitors)
                    {
                        var monitorKey = kvp.Key;
                        var entry = kvp.Value;

                        var state = new MonitorState
                        {
                            Brightness = entry.Brightness,
                            ColorTemperatureVcp = entry.ColorTemperatureVcp,
                            Contrast = entry.Contrast,
                            Volume = entry.Volume,
                        };

                        // Guarded like stateFile.Monitors above: an explicit JSON null lands here
                        // despite the member initializer, and the array can carry null elements.
                        // An unguarded dereference would escape to the method-level catch and drop
                        // every monitor after this one, which the next whole-file rewrite in
                        // BuildStateJson would then make permanent.
                        foreach (var feature in entry.KnownGoodVcpFeatures ?? Enumerable.Empty<KnownGoodVcpFeature>())
                        {
                            if (feature != null && feature.ToVcpFeatureValue().IsValid)
                            {
                                state.KnownGoodVcpFeatures[feature.Code] = feature.Clone();
                            }
                        }

                        _states[monitorKey] = state;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load monitor state: {ex.Message}");
            }
        }

        /// <summary>
        /// Marks the in-memory state dirty and schedules the debounced flush.
        /// </summary>
        /// <remarks>
        /// Dispose sets <c>_disposed</c> before it flushes, so a late caller can land here while the
        /// flush is still in flight. Either way the debouncer has already been disposed and would
        /// no-op, and re-setting <c>_isDirty</c> would only leave the instance permanently dirty with
        /// no path left to clear it, so late observations are dropped on purpose. The writer that can
        /// realistically arrive is the synchronous discovery block, whose observations the next
        /// discovery pass re-derives; <see cref="UpdateMonitorParameter"/> reaches the same path and
        /// its value would genuinely be lost, but Dispose runs only from App.Shutdown, immediately
        /// before the process exits.
        /// </remarks>
        private void MarkDirtyAndScheduleSave()
        {
            if (_disposed)
            {
                return;
            }

            _isDirty = true;
            _saveDebouncer.Debounce(SaveStateToDisk);
        }

        /// <summary>
        /// Serializes the current state and publishes the whole file. Both save paths go through here.
        /// </summary>
        /// <remarks>
        /// <c>_writeLock</c> serializes the two callers — the debounced save and the flush in
        /// <see cref="Dispose"/> — because cancelling the debouncer cannot reach a save already past
        /// its delay, and the write opens with <c>FileShare.Read</c>, so a colliding write loses at
        /// <c>CreateFile</c> and is dropped whole. <see cref="BuildStateJson"/> runs inside the lock,
        /// so the last snapshot built is the one that lands. Both paths write synchronously:
        /// <see cref="Dispose"/> has to flush without awaiting, the file is a few hundred bytes, and
        /// the async API was the only reason there were two write paths to collide in the first place.
        /// <para>
        /// The bytes go to a temp file and are renamed in, as <c>CrashDetectionScope</c> and
        /// <c>ProfileStore</c> do, because an in-place write truncates before it writes and not every
        /// exit path runs <see cref="Dispose"/> — the runner's Terminate event and its exit watchdog
        /// both call <c>Environment.Exit</c> outright. The rename keeps an interrupted write from
        /// emptying the file and dropping every monitor's state instead of just the last change.
        /// Deliberately no <c>Flush(flushToDisk: true)</c>: the threat here is process death, which
        /// the page cache survives, not power loss — and this flush runs on the UI thread at shutdown,
        /// where a FlushFileBuffers on a busy disk would be felt.
        /// </para>
        /// </remarks>
        internal void WriteStateFile()
        {
            lock (_writeLock)
            {
                var tempPath = _stateFilePath + ".tmp";
                File.WriteAllText(tempPath, BuildStateJson());
                File.Move(tempPath, _stateFilePath, overwrite: true);
            }
        }

        /// <summary>
        /// Writes the current state to disk. Called by the debouncer after the quiet period.
        /// </summary>
        private void SaveStateToDisk()
        {
            try
            {
                if (_disposed)
                {
                    return;
                }

                // Cleared before the snapshot, not after it: a change landing mid-write re-marks
                // dirty and is saved by the debounce that change schedules. Clearing afterwards
                // swallows that bit and Dispose then skips the flush entirely.
                _isDirty = false;
                WriteStateFile();
            }
            catch (Exception ex)
            {
                _isDirty = true;
                Logger.LogError($"Failed to save monitor state: {ex.Message}");
            }
        }

        /// <summary>
        /// Build the JSON string for state file.
        /// Called by <see cref="WriteStateFile"/> under the write lock.
        /// </summary>
        /// <returns>JSON string for state file</returns>
        private string BuildStateJson()
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

                lock (state)
                {
                    stateFile.Monitors[monitorId] = new MonitorStateEntry
                    {
                        Brightness = state.Brightness,
                        ColorTemperatureVcp = state.ColorTemperatureVcp,
                        Contrast = state.Contrast,
                        Volume = state.Volume,
                        KnownGoodVcpFeatures = state.KnownGoodVcpFeatures.Values
                            .OrderBy(feature => feature.Code)
                            .Select(feature => feature.Clone())
                            .ToList(),
                        LastUpdated = now,
                    };
                }
            }

            return JsonSerializer.Serialize(stateFile, MonitorStateSerializationContext.Default.MonitorStateFile);
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

            // Dispose the debouncer first so no further save is scheduled. A save already past its
            // delay is beyond its reach, which is what _writeLock covers.
            _saveDebouncer?.Dispose();

            if (wasDirty)
            {
                try
                {
                    WriteStateFile();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to flush monitor state on dispose: {ex.Message}");
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
