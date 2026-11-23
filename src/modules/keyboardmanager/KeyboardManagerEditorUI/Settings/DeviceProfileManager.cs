// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Settings
{
    /// <summary>
    /// Reads and writes deviceProfiles.json — the device→profile map and the auto-switch enable
    /// flag consumed by the engine for per-keyboard profile auto-switching. Saving signals the
    /// engine (via <see cref="ProfileManager.SignalEngineReload"/>) so it reloads the map.
    /// </summary>
    internal static class DeviceProfileManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        private static string FilePath => Path.Combine(KeyboardManagerPaths.SettingsDirectory, "deviceProfiles.json");

        private sealed class DeviceProfilesFile
        {
            public bool AutoSwitchEnabled { get; set; }

            public List<DeviceProfileEntry> Map { get; set; } = new();

            // The engine's profile-cycle hotkey definition. The editor doesn't own this (yet);
            // keep it as a raw node so saving from the editor round-trips it unchanged. Omitted when
            // unset: the engine treats a missing key as "no hotkey" but used to fail on an explicit null.
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public System.Text.Json.Nodes.JsonNode? CycleHotkey { get; set; }
        }

        private sealed class DeviceProfileEntry
        {
            public string Device { get; set; } = string.Empty;

            public string Profile { get; set; } = string.Empty;

            // Display name only; the engine ignores this field.
            public string Name { get; set; } = string.Empty;
        }

        public static bool GetAutoSwitchEnabled() => Load().AutoSwitchEnabled;

        /// <summary>Returns the saved keyboard→profile assignments (with display names).</summary>
        public static IReadOnlyList<DeviceAssignment> GetSavedAssignments()
        {
            return Load().Map
                .Where(e => !string.IsNullOrEmpty(e.Device) && !string.IsNullOrEmpty(e.Profile))
                .Select(e => new DeviceAssignment { Device = e.Device, Profile = e.Profile, Name = e.Name })
                .ToList();
        }

        /// <summary>
        /// Persists the auto-switch flag and assignments, then signals the engine to reload.
        /// Assignments with an empty device or profile are dropped (unassigned keyboards).
        /// </summary>
        public static bool Save(bool autoSwitchEnabled, IEnumerable<DeviceAssignment> assignments)
        {
            try
            {
                var file = new DeviceProfilesFile
                {
                    AutoSwitchEnabled = autoSwitchEnabled,
                    Map = assignments
                        .Where(a => !string.IsNullOrEmpty(a.Device) && !string.IsNullOrEmpty(a.Profile))
                        .Select(a => new DeviceProfileEntry { Device = a.Device, Profile = a.Profile, Name = a.Name })
                        .ToList(),
                    CycleHotkey = Load().CycleHotkey, // preserve the engine's hotkey definition
                };

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(file, _jsonOptions));

                ProfileManager.SignalEngineReload();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save deviceProfiles.json: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Removes every keyboard assignment that points at <paramref name="profile"/> and, if any
        /// were removed, rewrites the file and signals the engine. Called when a profile is deleted
        /// so no keyboard is left mapped to a profile that no longer exists (which would otherwise
        /// make auto-switch write a nonexistent profile into activeConfiguration on the next keystroke).
        /// Also called before a profile id is (re)created, so the new profile never inherits stale
        /// assignments. Returns false when the file could not be updated.
        /// </summary>
        public static bool RemoveAssignmentsForProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile))
            {
                return true;
            }

            try
            {
                DeviceProfilesFile file = Load();
                int removed = file.Map.RemoveAll(e => string.Equals(e.Profile, profile, StringComparison.Ordinal));
                if (removed == 0)
                {
                    return true;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(file, _jsonOptions));
                ProfileManager.SignalEngineReload();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to prune deviceProfiles.json for '{profile}': {ex.Message}");
                return false;
            }
        }

        private static DeviceProfilesFile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize<DeviceProfilesFile>(File.ReadAllText(FilePath), _jsonOptions)
                           ?? new DeviceProfilesFile();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to read deviceProfiles.json: " + ex.Message);
            }

            return new DeviceProfilesFile();
        }
    }
}
