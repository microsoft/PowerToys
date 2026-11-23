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

        // Win32 error codes reported when a create-new target already exists.
        private const int ErrorFileExists = 80;
        private const int ErrorAlreadyExists = 183;

        private static string SettingsJsonPath => Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>Folder holding the profile configs + settings.json — exposed so the editor can
        /// watch settings.json for engine-driven active-profile changes (auto-switch / cycle hotkey).</summary>
        public static string SettingsDirectory => KeyboardManagerPaths.SettingsDirectory;

        private static string ConfigPath(string profile) => Path.Combine(SettingsDirectory, profile + ".json");

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
                // Exclusive create: another editor's first save can create default.json between
                // any existence check and our write, and a plain write would truncate it.
                TryCreateFileExclusive(ConfigPath(DefaultProfile), EmptyConfigJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.EnsureDefaultProfileExists: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates <paramref name="path"/> with <paramref name="content"/> only if no file exists
        /// there yet. Returns false, leaving the existing file untouched, when another process
        /// created it first; any other I/O failure is thrown. Unlike a File.Exists check followed by
        /// a write, the create and the existence test are one atomic file-system operation.
        /// </summary>
        internal static bool TryCreateFileExclusive(string path, string content)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(content);
                return true;
            }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is ErrorFileExists or ErrorAlreadyExists)
            {
                return false;
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
                if (Directory.Exists(SettingsDirectory))
                {
                    foreach (string file in Directory.EnumerateFiles(SettingsDirectory, "*.json"))
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
        /// Returns false if the profile's config file does not exist (an empty one is created for
        /// the built-in "default" profile).
        /// </summary>
        public static bool SetActiveProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
            {
                return false;
            }

            try
            {
                // The picker always lists "default", but a fresh setup need not have default.json
                // yet (e.g. a profile was created before any default mapping was saved). Create it
                // empty so switching back to the built-in profile works.
                if (profile.Equals(DefaultProfile, StringComparison.OrdinalIgnoreCase))
                {
                    EnsureDefaultProfileExists();
                }

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

                // A profile that used this id before may have left mapping membership, keyboard
                // assignments or a display name behind (a delete from an older build, or one whose
                // last cleanup steps failed). Clear them first, so the new profile starts empty
                // instead of showing the old profile's turned-off rows or being auto-switched to.
                if (!SettingsManager.RemoveProfileMembership(profile) ||
                    !DeviceProfileManager.RemoveAssignmentsForProfile(profile) ||
                    !ProfileMetadataManager.Remove(profile))
                {
                    Logger.LogWarning($"ProfileManager.CreateProfile: could not clear what an earlier '{profile}' left behind");
                    return false;
                }

                // Both branches create the file exclusively, so if another editor creates the same
                // profile between the check above and here, this one fails instead of overwriting it.
                if (copyFromActive && File.Exists(ConfigPath(GetActiveProfile())))
                {
                    File.Copy(ConfigPath(GetActiveProfile()), target, overwrite: false);
                }
                else if (!TryCreateFileExclusive(target, EmptyConfigJson))
                {
                    Logger.LogWarning($"ProfileManager.CreateProfile: '{profile}' was created by another editor");
                    return false;
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
        /// Returns false when the profile could not be deleted. In that case the profile is kept:
        /// its {name}.json, its registration and its mappings' membership are as they were.
        /// </summary>
        public static bool DeleteProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile) || profile.Equals(DefaultProfile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string path = ConfigPath(profile);
            string? setAsidePath = null;
            EditorSettings? previousEditorSettings = null;
            bool committed = false;

            // Take the editor transaction lock BEFORE touching any file. If another editor holds it,
            // nothing has been changed yet and the delete fails cleanly. Holding it for the whole
            // delete, rollback included, also means no other editor can change the membership in
            // between, so a failure later on can be undone exactly.
            FileStream? transactionLock;
            try
            {
                transactionLock = SettingsManager.TryAcquireMappingTransactionLock();
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): {ex.Message}");
                return false;
            }

            if (transactionLock == null)
            {
                Logger.LogWarning($"ProfileManager.DeleteProfile('{profile}'): could not acquire the editor transaction lock.");
                return false;
            }

            using (transactionLock)
            {
                try
                {
                    // Move the config file aside instead of deleting it, so it can be put back if a later
                    // step fails. The dotted name hides it from GetProfiles() (IsReservedConfigStem), and
                    // the engine only opens {name}.json. If the file can't be moved (e.g. it is locked),
                    // abort before touching any other store.
                    if (File.Exists(path))
                    {
                        string target = Path.Combine(SettingsDirectory, $"{profile}.deleting-{Guid.NewGuid():N}.json");
                        File.Move(path, target);
                        setAsidePath = target;
                    }

                    if (!SettingsManager.RemoveProfileMembershipWhileLocked(profile, out previousEditorSettings))
                    {
                        // Keep the profile rather than leave its rows behind: with the file gone, a later
                        // profile reusing the id would show them as its own turned-off rows.
                        Logger.LogWarning($"ProfileManager.DeleteProfile('{profile}'): editor membership cleanup failed; keeping the profile.");
                        RestoreSetAsideFile(setAsidePath, path);
                        return false;
                    }

                    JsonObject root = (ReadSettingsRoot() as JsonObject) ?? CreateDefaultSettingsRoot();
                    JsonObject properties = EnsureObject(root, "properties");
                    RemoveFromConfigurationList(properties, profile);

                    // Decide the fallback from the state on disk RIGHT NOW, not from a snapshot taken at
                    // the top of this method: an engine auto-switch in another process can set
                    // activeConfiguration to this very profile in the window between that snapshot and
                    // here (the switch writes active=profile while its {name}.json still exists, then we
                    // delete the file). A stale "was it active when I started?" check misses that and would
                    // persist a just-deleted profile as active. Re-reading activeConfiguration here and
                    // normalizing when it names the deleted profile closes that interleaving; the mirror
                    // ordering (we delete first, the switch fires after) is refused by the engine's
                    // commit-time validation in SwitchActiveProfile. Basing the decision purely on the
                    // fresh value also avoids wrongly forcing "default" when an in-between switch moved the
                    // active profile to a different, still-valid one.
                    string? currentActive = properties["activeConfiguration"]?["value"]?.GetValue<string>();
                    bool activeIsDeleted = !string.IsNullOrEmpty(currentActive)
                        && currentActive.Equals(profile, StringComparison.OrdinalIgnoreCase);
                    if (activeIsDeleted)
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
                    committed = true;

                    if (activeIsDeleted)
                    {
                        SignalEngineReload();
                    }

                    // The profile is now deleted. The remaining stores only hold leftovers that
                    // CreateProfile purges before an id is reused, so a failure here is logged, not
                    // reported. Dropping the assignments keeps auto-switch from targeting the deleted
                    // profile (the engine also refuses a switch to a missing file); dropping the metadata
                    // keeps a later profile with the same id from inheriting the old display name.
                    bool assignmentsRemoved = DeviceProfileManager.RemoveAssignmentsForProfile(profile);
                    bool metadataRemoved = ProfileMetadataManager.Remove(profile);
                    if (!assignmentsRemoved || !metadataRemoved)
                    {
                        Logger.LogWarning($"ProfileManager.DeleteProfile('{profile}'): keyboard assignment or display-name cleanup did not complete.");
                    }

                    if (setAsidePath != null)
                    {
                        TryDeleteFile(setAsidePath);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): {ex.Message}");
                    if (committed)
                    {
                        return true;
                    }

                    // A step before settings.json was updated failed: put the membership and the file
                    // back so the profile stays exactly as it was. The lock is still held here.
                    if (previousEditorSettings != null && !SettingsManager.RestoreSettingsWhileLocked(previousEditorSettings))
                    {
                        Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): could not restore the editor membership.");
                    }

                    RestoreSetAsideFile(setAsidePath, path);
                    return false;
                }
            }
        }

        // Puts a config file moved aside by DeleteProfile back under its own name. If that fails, the
        // moved file is left in place (hidden, but not lost) and the failure is logged with its path.
        private static void RestoreSetAsideFile(string? setAsidePath, string path)
        {
            if (setAsidePath == null)
            {
                return;
            }

            try
            {
                File.Move(setAsidePath, path);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager: could not move '{setAsidePath}' back to '{path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Signals the KBM engine to reload its settings (picking up a changed activeConfiguration).
        /// A missing event is not fatal: the engine reads settings on next start.
        /// </summary>
        public static void SignalEngineReload()
        {
            // Unit tests work on a temporary folder; never make a running engine reload the user's
            // real settings because of them.
            if (KeyboardManagerPaths.IsRedirectedForTests)
            {
                return;
            }

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
            Directory.CreateDirectory(SettingsDirectory);
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
