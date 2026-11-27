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
        /// What the keyboard assignment dialog showed when it opened: the auto-switch flag and one
        /// entry per keyboard row, with an empty <see cref="DeviceAssignment.Profile"/> for a
        /// keyboard shown as not assigned. <see cref="SaveChanges(DeviceAssignmentsBaseline, bool, IReadOnlyList{DeviceAssignment})"/>
        /// writes only what the user changed relative to it.
        /// </summary>
        public sealed record DeviceAssignmentsBaseline(bool AutoSwitchEnabled, IReadOnlyList<DeviceAssignment> Assignments);

        /// <summary>Outcome of <see cref="SaveChanges(DeviceAssignmentsBaseline, bool, IReadOnlyList{DeviceAssignment})"/>.</summary>
        public enum DeviceAssignmentsSaveResult
        {
            Saved,

            /// <summary>
            /// Saved, but keyboards the user assigned in this dialog to a profile that was deleted
            /// meanwhile were left out. The out parameter of SaveChanges names those profiles.
            /// </summary>
            SavedWithDroppedAssignments,

            /// <summary>Another editor held the transaction lock for too long. Nothing was written.</summary>
            Busy,

            /// <summary>The current file could not be read, so it was not overwritten.</summary>
            Unreadable,

            /// <summary>The file could not be written. It is unchanged.</summary>
            Failed,
        }

        /// <summary>The saved flag and assignments, as a dialog that shows all of them would start.</summary>
        internal static DeviceAssignmentsBaseline LoadBaseline() =>
            new(GetAutoSwitchEnabled(), GetSavedAssignments());

        /// <summary>
        /// Saves the keyboard assignment dialog. The dialog can stay open for minutes, and another
        /// editor (the Settings page and the runner can each start one) may have saved in the
        /// meantime, so the dialog's rows are not written as they are. Under the editor transaction
        /// lock (also held by profile create and delete while they prune assignments) the current
        /// file is read again and only this dialog's changes from <paramref name="baseline"/> are
        /// applied: per keyboard, an assignment that was added, changed or cleared, and the
        /// auto-switch flag only if the user changed it. Assignments to profiles that no longer
        /// exist are dropped. The file is replaced atomically, and the engine is signalled.
        /// </summary>
        public static DeviceAssignmentsSaveResult SaveChanges(DeviceAssignmentsBaseline baseline, bool autoSwitchEnabled, IReadOnlyList<DeviceAssignment> edited) =>
            SaveChanges(baseline, autoSwitchEnabled, edited, lockTimeoutMS: 10_000, out _);

        /// <inheritdoc cref="SaveChanges(DeviceAssignmentsBaseline, bool, IReadOnlyList{DeviceAssignment})"/>
        /// <param name="droppedProfiles">The profiles of the assignments the user made in this
        /// dialog that were left out because the profile no longer exists. Empty unless the result
        /// is <see cref="DeviceAssignmentsSaveResult.SavedWithDroppedAssignments"/>.</param>
        public static DeviceAssignmentsSaveResult SaveChanges(DeviceAssignmentsBaseline baseline, bool autoSwitchEnabled, IReadOnlyList<DeviceAssignment> edited, out IReadOnlyList<string> droppedProfiles) =>
            SaveChanges(baseline, autoSwitchEnabled, edited, lockTimeoutMS: 10_000, out droppedProfiles);

        /// <inheritdoc cref="SaveChanges(DeviceAssignmentsBaseline, bool, IReadOnlyList{DeviceAssignment})"/>
        internal static DeviceAssignmentsSaveResult SaveChanges(DeviceAssignmentsBaseline baseline, bool autoSwitchEnabled, IReadOnlyList<DeviceAssignment> edited, int lockTimeoutMS) =>
            SaveChanges(baseline, autoSwitchEnabled, edited, lockTimeoutMS, out _);

        /// <inheritdoc cref="SaveChanges(DeviceAssignmentsBaseline, bool, IReadOnlyList{DeviceAssignment}, out IReadOnlyList{string})"/>
        internal static DeviceAssignmentsSaveResult SaveChanges(DeviceAssignmentsBaseline baseline, bool autoSwitchEnabled, IReadOnlyList<DeviceAssignment> edited, int lockTimeoutMS, out IReadOnlyList<string> droppedProfiles)
        {
            droppedProfiles = Array.Empty<string>();
            try
            {
                using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock(lockTimeoutMS);
                if (transactionLock == null)
                {
                    Logger.LogWarning("DeviceProfileManager.SaveChanges: could not acquire the editor transaction lock.");
                    return DeviceAssignmentsSaveResult.Busy;
                }

                if (!TryLoadForUpdate(out DeviceProfilesFile file))
                {
                    return DeviceAssignmentsSaveResult.Unreadable;
                }

                // Profile ids are file names, so ids differing only in case are one profile.
                var existingProfiles = new HashSet<string>(ProfileManager.GetProfiles(), StringComparer.OrdinalIgnoreCase);
                List<DeviceAssignment> current = file.Map
                    .Select(e => new DeviceAssignment { Device = e.Device, Profile = e.Profile, Name = e.Name })
                    .ToList();
                file.Map = MergeAssignments(current, baseline.Assignments, edited, existingProfiles.Contains, out List<string> dropped)
                    .Select(a => new DeviceProfileEntry { Device = a.Device, Profile = a.Profile, Name = a.Name })
                    .ToList();
                if (autoSwitchEnabled != baseline.AutoSwitchEnabled)
                {
                    file.AutoSwitchEnabled = autoSwitchEnabled;
                }

                WriteFile(file);
                ProfileManager.SignalEngineReload();
                if (dropped.Count > 0)
                {
                    droppedProfiles = dropped;
                    return DeviceAssignmentsSaveResult.SavedWithDroppedAssignments;
                }

                return DeviceAssignmentsSaveResult.Saved;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save deviceProfiles.json: " + ex.Message);
                return DeviceAssignmentsSaveResult.Failed;
            }
        }

        /// <summary>
        /// Applies the dialog's per-keyboard changes (<paramref name="baseline"/> to
        /// <paramref name="edited"/>; an empty profile means not assigned) to
        /// <paramref name="current"/>, the assignments as they are on disk now. A keyboard whose
        /// profile the user changed is set or, when cleared, removed (a change to a profile that no
        /// longer exists is rejected and leaves the keyboard's entry as it is); a keyboard the user did not
        /// change keeps whatever <paramref name="current"/> has, so another editor's newer save of it
        /// survives. A display name refreshed by typing is updated only where the assignment itself
        /// is unchanged. Entries with an empty device or profile, or naming a profile for which
        /// <paramref name="profileExists"/> is false, are dropped. <paramref name="droppedChanges"/>
        /// lists, once each, the profiles of the assignments the user changed in the dialog that were
        /// dropped that way; stale entries already in the file are dropped silently.
        /// </summary>
        internal static List<DeviceAssignment> MergeAssignments(
            IReadOnlyList<DeviceAssignment> current,
            IReadOnlyList<DeviceAssignment> baseline,
            IReadOnlyList<DeviceAssignment> edited,
            Func<string, bool> profileExists,
            out List<string> droppedChanges)
        {
            droppedChanges = new List<string>();
            var result = current
                .Where(a => !string.IsNullOrEmpty(a.Device) && !string.IsNullOrEmpty(a.Profile))
                .Select(a => new DeviceAssignment { Device = a.Device, Profile = a.Profile, Name = a.Name })
                .ToList();

            var shown = new Dictionary<string, DeviceAssignment>(StringComparer.OrdinalIgnoreCase);
            foreach (DeviceAssignment row in baseline)
            {
                if (!string.IsNullOrEmpty(row.Device))
                {
                    shown[row.Device] = row;
                }
            }

            foreach (DeviceAssignment row in edited)
            {
                if (string.IsNullOrEmpty(row.Device))
                {
                    continue;
                }

                string shownProfile = shown.TryGetValue(row.Device, out DeviceAssignment? before) ? before.Profile ?? string.Empty : string.Empty;
                string editedProfile = row.Profile ?? string.Empty;
                int index = result.FindIndex(a => string.Equals(a.Device, row.Device, StringComparison.OrdinalIgnoreCase));

                // Profile ids are compared without letter case: "work" in the file and "Work" in the
                // dialog are the same profile, so such a row was not changed by the user.
                if (!string.Equals(shownProfile, editedProfile, StringComparison.OrdinalIgnoreCase))
                {
                    // Check the new profile before touching the keyboard's entry: a change that is
                    // rejected must leave the assignment the file has (e.g. a valid "Work") in place.
                    if (editedProfile.Length > 0 && !profileExists(editedProfile))
                    {
                        if (!droppedChanges.Contains(editedProfile, StringComparer.Ordinal))
                        {
                            droppedChanges.Add(editedProfile);
                        }

                        continue;
                    }

                    if (index >= 0)
                    {
                        result.RemoveAt(index);
                    }

                    if (editedProfile.Length > 0)
                    {
                        result.Insert(index >= 0 ? index : result.Count, new DeviceAssignment { Device = row.Device, Profile = editedProfile, Name = row.Name });
                    }
                }
                else if (index >= 0 &&
                    editedProfile.Length > 0 &&
                    string.Equals(result[index].Profile, editedProfile, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(row.Name))
                {
                    result[index].Name = row.Name;
                }
            }

            result.RemoveAll(a => !profileExists(a.Profile));
            return result;
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

                // Ignore case: the engine opens "<profile>.json" on a file system that ignores case,
                // so "work" and "Work" are the same profile to it.
                int removed = file.Map.RemoveAll(e => string.Equals(e.Profile, profile, StringComparison.OrdinalIgnoreCase));
                if (removed == 0)
                {
                    return true;
                }

                WriteFile(file);
                ProfileManager.SignalEngineReload();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to prune deviceProfiles.json for '{profile}': {ex.Message}");
                return false;
            }
        }

        // Reads the file for a read-modify-write. Unlike Load(), a file that exists but cannot be
        // read is a failure: writing defaults over it would drop every assignment and the hotkey.
        private static bool TryLoadForUpdate(out DeviceProfilesFile file)
        {
            file = new DeviceProfilesFile();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return true;
                }

                DeviceProfilesFile? loaded = JsonSerializer.Deserialize<DeviceProfilesFile>(File.ReadAllText(FilePath), _jsonOptions);
                if (loaded == null)
                {
                    Logger.LogWarning("deviceProfiles.json is empty or null; not overwriting it.");
                    return false;
                }

                loaded.Map ??= new List<DeviceProfileEntry>();
                file = loaded;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to read deviceProfiles.json for an update: " + ex.Message);
                return false;
            }
        }

        // Replaces the file atomically (write a temporary file, then move it over), so the engine
        // or another editor never reads a half-written file.
        private static void WriteFile(DeviceProfilesFile file)
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, $"deviceProfiles.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(file, _jsonOptions));
                SettingsFileReplacer.Replace(temporaryPath, FilePath);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception)
                {
                }
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
