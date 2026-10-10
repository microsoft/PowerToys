// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Settings
{
    /// <summary>
    /// Manages Keyboard Manager profiles. Each profile is one engine config file "{name}.json"
    /// in the Keyboard Manager folder; the active profile is recorded in settings.json under
    /// "activeConfiguration" and listed in "keyboardConfigurations". Switching the active profile
    /// rewrites those values and signals the engine's reload event, which live-swaps the whole
    /// active remap set (verified: the engine re-reads activeConfiguration on that event).
    /// </summary>
    internal static class ProfileManager
    {
        private const string DefaultProfile = "default";

        // Named event the KBM engine waits on; signaling it makes the engine reload its settings.
        private const string SettingsChangedEventName = "PowerToys_KeyboardManager_Event_Settings";

        // A valid but empty engine config, used when creating a fresh profile.
        private const string EmptyConfigJson =
            "{\"remapKeys\":{\"inProcess\":[]},\"remapKeysToText\":{\"inProcess\":[]}," +
            "\"remapShortcuts\":{\"global\":[],\"appSpecific\":[]}," +
            "\"remapShortcutsToText\":{\"global\":[],\"appSpecific\":[]}}";

        private static readonly string _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager");

        private static string SettingsJsonPath => Path.Combine(_settingsDirectory, "settings.json");

        /// <summary>Folder holding the profile configs + settings.json — exposed so the editor can
        /// watch settings.json for engine-driven active-profile changes (auto-switch / cycle hotkey).</summary>
        public static string SettingsDirectory => _settingsDirectory;

        private static string ConfigPath(string profile) => Path.Combine(_settingsDirectory, profile + ".json");

        /// <summary>
        /// Creates an empty default.json only when it is missing; never overwrites an existing one
        /// (its mappings must be preserved). "default" is the engine's fallback profile, but on a
        /// fresh setup its file need not exist yet, so any code path that points activeConfiguration
        /// at "default" must first make sure the file is there for the engine to load.
        /// </summary>
        private static void EnsureDefaultProfileExists()
        {
            try
            {
                string path = ConfigPath(DefaultProfile);
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, EmptyConfigJson);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.EnsureDefaultProfileExists: {ex.Message}");
            }
        }

        // Files that live in the save folder but are NOT remap-profile configs and must never
        // be surfaced as selectable profiles: the module settings, the editor's per-profile
        // caches ("editorSettings*"), the device->profile auto-switch map ("deviceProfiles"),
        // and dotted backups like "default.backup-...". Kept in one place so the discovery scan
        // and the name validator can never drift apart.
        private static bool IsReservedConfigStem(string stem) =>
            stem.Contains('.') ||
            stem.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
            stem.StartsWith("editorSettings", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("deviceProfiles", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("profileMetadata", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the known profile names. Reads "keyboardConfigurations" from settings.json and
        /// also scans "{name}.json" files so externally created profiles are picked up. Always
        /// includes the built-in "default".
        /// </summary>
        public static IReadOnlyList<string> GetProfiles()
        {
            var names = new List<string>();

            try
            {
                if (ReadSettingsRoot()?["properties"]?["keyboardConfigurations"]?["value"] is JsonArray configs)
                {
                    foreach (JsonNode? item in configs)
                    {
                        string? name = item?.GetValue<string>();
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager.GetProfiles: failed reading settings.json: {ex.Message}");
            }

            try
            {
                if (Directory.Exists(_settingsDirectory))
                {
                    foreach (string file in Directory.EnumerateFiles(_settingsDirectory, "*.json"))
                    {
                        string stem = Path.GetFileNameWithoutExtension(file);

                        // Skip settings.json, editor caches, the device map, and dotted backups.
                        if (IsReservedConfigStem(stem))
                        {
                            continue;
                        }

                        if (!names.Contains(stem))
                        {
                            names.Add(stem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager.GetProfiles: failed scanning config files: {ex.Message}");
            }

            if (!names.Contains(DefaultProfile))
            {
                names.Insert(0, DefaultProfile);
            }

            return names;
        }

        /// <summary>
        /// A profile as the UI should present it: <see cref="Id"/> is the stable identity (the config
        /// filename stem, used in activeConfiguration / keyboardConfigurations / mapping membership /
        /// device assignments), and <see cref="DisplayName"/> is the editable label shown to the user
        /// (equal to the id until it has been renamed).
        /// </summary>
        public readonly record struct ProfileInfo(string Id, string DisplayName);

        /// <summary>
        /// Returns the known profiles as (id, display name) pairs, in the same order as
        /// <see cref="GetProfiles"/>. The display name falls back to the id when none has been set.
        /// </summary>
        public static IReadOnlyList<ProfileInfo> GetProfilesWithDisplayNames()
        {
            var result = new List<ProfileInfo>();
            foreach (string id in GetProfiles())
            {
                result.Add(new ProfileInfo(id, ProfileMetadataManager.GetDisplayName(id)));
            }

            return result;
        }

        /// <summary>Gets the editable display name for a profile id (falls back to the id itself).</summary>
        public static string GetDisplayName(string id) => ProfileMetadataManager.GetDisplayName(id);

        /// <summary>
        /// Renames a profile by changing only its display name. The profile's id (its {id}.json file
        /// and every reference in settings.json / mapping membership / device assignments) is left
        /// untouched, so a rename can never orphan a config or silently break auto-switching — the cost
        /// that made a filename-based rename risky. The new name is a free-form label; it need not be a
        /// valid filename because it is never used as one. Returns false for a blank name.
        /// </summary>
        public static bool RenameProfile(string id, string newDisplayName)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newDisplayName))
            {
                return false;
            }

            return ProfileMetadataManager.SetDisplayName(id, newDisplayName.Trim());
        }

        /// <summary>Gets the active profile name (defaults to "default").</summary>
        public static string GetActiveProfile()
        {
            try
            {
                string? active = ReadSettingsRoot()?["properties"]?["activeConfiguration"]?["value"]?.GetValue<string>();
                return string.IsNullOrEmpty(active) ? DefaultProfile : active;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager.GetActiveProfile: {ex.Message}");
                return DefaultProfile;
            }
        }

        /// <summary>
        /// Makes <paramref name="profile"/> the active profile and signals the engine to reload.
        /// Returns false if the profile's config file does not exist.
        /// </summary>
        public static bool SetActiveProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
            {
                return false;
            }

            try
            {
                if (!File.Exists(ConfigPath(profile)))
                {
                    Logger.LogWarning($"ProfileManager.SetActiveProfile: '{profile}.json' does not exist");
                    return false;
                }

                JsonObject root = (ReadSettingsRoot() as JsonObject) ?? CreateDefaultSettingsRoot();
                JsonObject properties = EnsureObject(root, "properties");
                SetValueProperty(properties, "activeConfiguration", profile);
                AddToConfigurationList(properties, profile);
                WriteSettingsRoot(root);

                SignalEngineReload();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.SetActiveProfile('{profile}'): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new profile config file (empty, or copied from the current active profile) and
        /// registers it in settings.json. Does not switch to it. Returns false on invalid name or if
        /// a profile with that name already exists.
        /// </summary>
        public static bool CreateProfile(string profile, bool copyFromActive)
        {
            if (!IsValidProfileName(profile))
            {
                Logger.LogWarning($"ProfileManager.CreateProfile: invalid name '{profile}'");
                return false;
            }

            try
            {
                string target = ConfigPath(profile);
                if (File.Exists(target))
                {
                    Logger.LogWarning($"ProfileManager.CreateProfile: '{profile}' already exists");
                    return false;
                }

                if (copyFromActive && File.Exists(ConfigPath(GetActiveProfile())))
                {
                    File.Copy(ConfigPath(GetActiveProfile()), target, overwrite: false);
                }
                else
                {
                    File.WriteAllText(target, EmptyConfigJson);
                }

                JsonObject root = (ReadSettingsRoot() as JsonObject) ?? CreateDefaultSettingsRoot();
                JsonObject properties = EnsureObject(root, "properties");
                AddToConfigurationList(properties, profile);
                WriteSettingsRoot(root);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.CreateProfile('{profile}'): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a profile's config file and unregisters it, dropping the profile from the editor
        /// settings' membership as well. The built-in "default" profile cannot be deleted. If the
        /// deleted profile was active, switches to "default" and signals a reload.
        /// </summary>
        public static bool DeleteProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile) || profile.Equals(DefaultProfile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                bool wasActive = GetActiveProfile().Equals(profile, StringComparison.OrdinalIgnoreCase);

                // Abort before touching any other store if the config file can't be removed:
                // otherwise we'd unregister the profile while its {name}.json stays on disk, and
                // the directory scan in GetProfiles() would resurrect it on the next refresh.
                if (!TryDeleteFile(ConfigPath(profile)))
                {
                    return false;
                }

                if (!SettingsManager.RemoveProfileMembership(profile))
                {
                    // The config file is already deleted, so we can't cleanly abort; log and continue
                    // the remaining cleanup. A failure here means a concurrent editor held the lock or
                    // the write failed, leaving stale editor membership metadata for a profile whose
                    // file is gone — harmless to the engine, and reconciled away on the next load.
                    Logger.LogWarning($"ProfileManager.DeleteProfile('{profile}'): editor membership cleanup did not complete.");
                }

                // Drop any keyboard->profile assignments for the deleted profile so auto-switch
                // never targets a profile that no longer exists.
                DeviceProfileManager.RemoveAssignmentsForProfile(profile);

                // Drop the display-name metadata for the deleted id so a later profile that happens
                // to reuse the id doesn't inherit a stale name.
                ProfileMetadataManager.Remove(profile);

                JsonObject root = (ReadSettingsRoot() as JsonObject) ?? CreateDefaultSettingsRoot();
                JsonObject properties = EnsureObject(root, "properties");
                RemoveFromConfigurationList(properties, profile);
                if (wasActive)
                {
                    // Guarantee the fallback target exists before we point activeConfiguration at it.
                    // The engine resolves activeConfiguration by opening {name}.json and, on a failed
                    // read, keeps its previous in-memory mappings — so falling back to "default" with
                    // no default.json would leave the just-deleted profile's remaps live while the
                    // editor shows an empty default.
                    EnsureDefaultProfileExists();
                    SetValueProperty(properties, "activeConfiguration", DefaultProfile);
                }

                WriteSettingsRoot(root);

                if (wasActive)
                {
                    SignalEngineReload();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Signals the KBM engine to reload its settings (picking up a changed activeConfiguration).
        /// A missing event is not fatal: the engine reads settings on next start.
        /// </summary>
        public static void SignalEngineReload()
        {
            try
            {
                using EventWaitHandle handle = EventWaitHandle.OpenExisting(SettingsChangedEventName);
                handle.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Logger.LogInfo("ProfileManager.SignalEngineReload: engine reload event not present (engine not running?)");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager.SignalEngineReload: {ex.Message}");
            }
        }

        private static bool IsValidProfileName(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
            {
                return false;
            }

            // Reject reserved names (settings / editor caches / device map / dotted backups)
            // and any name with filesystem-invalid characters.
            if (profile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || IsReservedConfigStem(profile))
            {
                return false;
            }

            return true;
        }

        private static JsonNode? ReadSettingsRoot()
        {
            return File.Exists(SettingsJsonPath) ? JsonNode.Parse(File.ReadAllText(SettingsJsonPath)) : null;
        }

        private static JsonObject CreateDefaultSettingsRoot()
        {
            return new JsonObject
            {
                ["properties"] = new JsonObject(),
                ["name"] = "Keyboard Manager",
                ["version"] = "1.0",
            };
        }

        private static void WriteSettingsRoot(JsonNode root)
        {
            Directory.CreateDirectory(_settingsDirectory);
            File.WriteAllText(SettingsJsonPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }

        private static JsonObject EnsureObject(JsonObject parent, string key)
        {
            if (parent[key] is JsonObject existing)
            {
                return existing;
            }

            var created = new JsonObject();
            parent[key] = created;
            return created;
        }

        private static void SetValueProperty(JsonObject properties, string key, string value)
        {
            EnsureObject(properties, key)["value"] = value;
        }

        private static void AddToConfigurationList(JsonObject properties, string profile)
        {
            JsonObject holder = EnsureObject(properties, "keyboardConfigurations");
            if (holder["value"] is not JsonArray array)
            {
                array = new JsonArray();
                holder["value"] = array;
            }

            if (!array.Any(n => string.Equals(n?.GetValue<string>(), profile, StringComparison.Ordinal)))
            {
                array.Add(profile);
            }
        }

        private static void RemoveFromConfigurationList(JsonObject properties, string profile)
        {
            if (properties["keyboardConfigurations"]?["value"] is not JsonArray array)
            {
                return;
            }

            for (int i = array.Count - 1; i >= 0; i--)
            {
                if (string.Equals(array[i]?.GetValue<string>(), profile, StringComparison.Ordinal))
                {
                    array.RemoveAt(i);
                }
            }
        }

        // Returns true when the file is gone after the call (deleted, or never existed);
        // false when a delete was attempted but failed (e.g. the file is locked), so callers
        // can abort before unregistering a profile whose config is still on disk.
        private static bool TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager: failed deleting '{path}': {ex.Message}");
                return false;
            }
        }
    }
}
