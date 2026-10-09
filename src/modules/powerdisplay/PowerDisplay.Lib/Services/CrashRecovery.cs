// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ManagedCommon;
using PowerDisplay.Common;
using PowerDisplay.Common.Utils;
using PowerToys.Interop;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Detects crash evidence at PowerDisplay.exe startup and runs the strict auto-disable
    /// sequence when an orphan <c>discovery.lock</c> is found.
    ///
    /// The sequence is: write crash_detected.flag → write settings.json → signal event →
    /// delete lock. Any step failure throws and leaves the lock in place; next startup
    /// retries the entire sequence. This "lock-as-commit-point" pattern is what makes
    /// the mechanism self-healing.
    /// </summary>
    public sealed partial class CrashRecovery
    {
        private readonly string _lockPath;
        private readonly string _flagPath;
        private readonly string _settingsPath;
        private readonly Func<string, bool> _signalEvent;

        public CrashRecovery(string lockPath, string flagPath, string settingsPath, Func<string, bool> signalEvent)
        {
            _lockPath = lockPath;
            _flagPath = flagPath;
            _settingsPath = settingsPath;
            _signalEvent = signalEvent;
        }

        /// <summary>
        /// Production constructor. Uses <see cref="PathConstants"/> defaults and the real
        /// <see cref="EventHelper.SignalEvent"/>. The auto-disable event name comes from
        /// the WinRT <c>Constants</c> projection.
        /// </summary>
        public static CrashRecovery CreateDefault()
        {
            return new CrashRecovery(
                lockPath: PathConstants.DiscoveryLockPath,
                flagPath: PathConstants.CrashDetectedFlagPath,
                settingsPath: PathConstants.GlobalPowerToysSettingsPath,
                signalEvent: name => EventHelper.SignalEvent(name));
        }

        /// <summary>
        /// Returns true if an orphan lock was found and the auto-disable sequence executed.
        /// Caller should exit the process when this returns true.
        /// Throws on any sequence step failure (strict fail-fast). Lock is preserved on
        /// failure so the next startup retries.
        /// </summary>
        public bool DetectOrphanAndDisable()
        {
            if (!File.Exists(_lockPath))
            {
                Logger.LogTrace("Phase 0: no orphan lock; normal startup");
                return false;
            }

            string lockContent = SafeReadAllText(_lockPath) ?? "<unreadable>";
            Logger.LogWarning($"Phase 0: found orphan lock at {_lockPath} with content {lockContent}; entering auto-disable sequence");

            WriteCrashFlag();
            Logger.LogInfo("Phase 0: step 1 (write crash_detected.flag) ok");

            WriteSettingsDisabled();
            Logger.LogInfo("Phase 0: step 2 (write settings.json) ok");

            SignalAutoDisable();
            Logger.LogInfo("Phase 0: step 3 (signal AutoDisable event) ok");

            // Commit point — last on purpose so any earlier failure leaves the lock for retry.
            File.Delete(_lockPath);
            Logger.LogInfo("Phase 0: step 4 (delete discovery.lock) ok — sequence committed");

            return true;
        }

        private void WriteCrashFlag()
        {
            var dir = Path.GetDirectoryName(_flagPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = new CrashFlagPayload(
                Version: 1,
                DetectedAt: DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, CrashFlagJsonContext.Default.CrashFlagPayload);
            File.WriteAllBytes(_flagPath, bytes);
        }

        private void WriteSettingsDisabled()
        {
            // JsonNode is AOT-safe (no reflection-based deserialization).
            var text = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : "{}";
            var root = JsonNode.Parse(text) ?? new JsonObject();

            var enabled = root["enabled"] as JsonObject ?? new JsonObject();
            enabled["PowerDisplay"] = false;
            root["enabled"] = enabled;

            // Compact format matches Runner's json::to_file output; indented would churn settings.json on every Phase 0.
            File.WriteAllText(_settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }

        private void SignalAutoDisable()
        {
            // Read the event name from the WinRT projection of shared_constants.h so the
            // C++ side stays the single source of truth (no parallel C# copy to keep in sync).
            var eventName = Constants.AutoDisablePowerDisplayEvent();
            if (!_signalEvent(eventName))
            {
                throw new InvalidOperationException($"Failed to signal {eventName}");
            }
        }

        private static string? SafeReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        // AOT-safe JSON serialization for the crash flag payload.
        internal sealed record CrashFlagPayload(int Version, string DetectedAt);

        [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(CrashFlagPayload))]
        internal sealed partial class CrashFlagJsonContext : JsonSerializerContext
        {
        }
    }
}
