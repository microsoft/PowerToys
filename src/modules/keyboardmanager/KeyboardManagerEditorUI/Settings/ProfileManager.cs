// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
        /// <summary>How <see cref="DeleteProfileWithResult(string)"/> ended.</summary>
        public enum ProfileDeleteResult
        {
            /// <summary>The profile was deleted.</summary>
            Deleted,

            /// <summary>The profile could not be deleted and was kept exactly as it was.</summary>
            Kept,

            /// <summary>The profile could not be deleted, and undoing the steps already done failed
            /// too, so part of it (its file or some mapping membership) may be missing.</summary>
            NotFullyRestored,
        }

        internal const string DefaultProfile = "default";

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

        // A config file moved aside by DeleteProfile: "{id}.deleting-<guid "N" format>.json", or
        // "{id}.deleting-unregistered-<guid>.json" for a profile that was not registered in
        // settings.json (a "{name}.json" file that GetProfiles found by scanning the folder).
        private static readonly Regex SetAsideFileName = new(
            @"^(?<id>[^.]+)\.deleting-(?<unregistered>unregistered-)?[0-9a-f]{32}\.json$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Lock wait used by the UI; tests pass a shorter one.
        private const int DefaultLockTimeoutMS = 10_000;

        private static string SettingsJsonPath => Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>Gets or sets an action run by CreateProfile right after it checked that the
        /// profile does not exist yet. Unit tests use it to create the file there, as another editor
        /// could. Always null in the product.</summary>
        internal static Action? AfterCreateExistenceCheckForTests { get; set; }

        /// <summary>Gets or sets an action run by DeleteProfile just before it writes settings.json.
        /// Unit tests use it to make later steps fail. Always null in the product.</summary>
        internal static Action? BeforeDeleteCommitForTests { get; set; }

        /// <summary>Gets or sets the number of engine reloads requested while the folder was redirected
        /// for tests (and therefore not sent).</summary>
        internal static int SuppressedEngineReloadsForTests { get; set; }

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

            RecoverSetAsideFiles();

            try
            {
                if (ReadSettingsRoot()?["properties"]?["keyboardConfigurations"]?["value"] is JsonArray configs)
                {
                    foreach (JsonNode? item in configs)
                    {
                        string? name = item?.GetValue<string>();
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
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

                        // Profile ids are file names, so ids differing only in case are one profile.
                        if (!names.Contains(stem, StringComparer.OrdinalIgnoreCase))
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

            if (!names.Contains(DefaultProfile, StringComparer.OrdinalIgnoreCase))
            {
                names.Insert(0, DefaultProfile);
            }

            return names;
        }

        // The moved-aside config files ("{id}.deleting-[unregistered-]<guid>.json") in the folder,
        // with their ids and whether the profile was unregistered when it was deleted.
        private static IEnumerable<(string Path, string Id, bool WasUnregistered)> FindSetAsideFiles()
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(SettingsDirectory, "*.deleting-*.json"))
            {
                Match match = SetAsideFileName.Match(Path.GetFileName(file));
                if (match.Success)
                {
                    yield return (file, match.Groups["id"].Value, match.Groups["unregistered"].Success);
                }
            }
        }

        /// <summary>
        /// Cleans up config files that DeleteProfile moved aside and never finished with: the process
        /// ended mid-delete, or a step afterwards failed. Only set-aside files whose "{id}.json" is
        /// absent are touched:
        /// <list type="bullet">
        /// <item>The id is still registered in settings.json: the delete did not commit, so the file
        /// is moved back.</item>
        /// <item>The profile was not registered when it was deleted (its file name says so): nothing
        /// on disk tells whether that delete committed, so the file is moved back. Restoring a
        /// deleted profile only means deleting it again; removing a kept one would lose it.</item>
        /// <item>Otherwise the delete unregistered the profile, so it committed, and the file is
        /// removed.</item>
        /// </list>
        /// Runs only when the editor transaction lock is free right now, because a delete in progress
        /// holds it while its file is moved aside.
        /// </summary>
        private static void RecoverSetAsideFiles()
        {
            try
            {
                var leftovers = FindSetAsideFiles()
                    .Where(f => !File.Exists(ConfigPath(f.Id)))
                    .ToList();
                if (leftovers.Count == 0)
                {
                    return;
                }

                using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock(timeoutMS: 0);
                if (transactionLock == null)
                {
                    return;
                }

                HashSet<string> registered = ReadRegisteredProfiles();
                foreach ((string file, string id, bool wasUnregistered) in leftovers)
                {
                    string path = ConfigPath(id);
                    if (File.Exists(path))
                    {
                        continue; // an earlier leftover of the same id was just moved back
                    }

                    if (registered.Contains(id) || wasUnregistered)
                    {
                        Logger.LogWarning($"ProfileManager: restoring '{file}', left behind by a delete that did not finish.");
                        RestoreSetAsideFile(file, path);
                    }
                    else
                    {
                        Logger.LogWarning($"ProfileManager: removing '{file}', left behind by a finished delete.");
                        TryDeleteFile(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager: could not clean up moved-aside profile files: {ex.Message}");
            }
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
        /// Reads the active profile for callers that must not act on a guess (a live reload):
        /// returns false when settings.json exists but cannot be read or parsed right now, where
        /// <see cref="GetActiveProfile"/> would answer "default". No settings.json at all means
        /// "default" is active, as it does there.
        /// </summary>
        internal static bool TryGetActiveProfile(out string profile)
        {
            try
            {
                string? active = ReadSettingsRoot()?["properties"]?["activeConfiguration"]?["value"]?.GetValue<string>();
                profile = string.IsNullOrEmpty(active) ? DefaultProfile : active;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager.TryGetActiveProfile: {ex.Message}");
                profile = DefaultProfile;
                return false;
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

                // Under the transaction lock, like a delete: otherwise another editor window could
                // delete this profile between the check below and the write, and the write would
                // put the deleted profile back as registered and active.
                using (FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock())
                {
                    if (transactionLock == null)
                    {
                        Logger.LogWarning("ProfileManager.SetActiveProfile: could not acquire the editor transaction lock.");
                        return false;
                    }

                    if (!File.Exists(ConfigPath(profile)))
                    {
                        Logger.LogWarning("ProfileManager.SetActiveProfile: the profile's file does not exist");
                        return false;
                    }

                    JsonObject root = (ReadSettingsRoot() as JsonObject) ?? CreateDefaultSettingsRoot();
                    JsonObject properties = EnsureObject(root, "properties");
                    SetValueProperty(properties, "activeConfiguration", profile);
                    AddToConfigurationList(properties, profile);
                    WriteSettingsRoot(root);
                }

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
        public static bool CreateProfile(string profile, bool copyFromActive) =>
            CreateProfile(profile, copyFromActive ? GetActiveProfile() : null);

        /// <summary>
        /// Creates a new profile config file and registers it in settings.json. Does not switch to it.
        /// <paramref name="copyFromProfile"/> names the profile to copy, or is null for an empty
        /// profile. Callers with a dialog pass the profile captured when it opened, so a profile switch
        /// while it is open does not change what is copied. Returns false on an invalid name (including
        /// "default" in any case), if a profile with that name already exists (ignoring case), if the
        /// profile to copy no longer exists ("default" without a file yet is copied as an empty
        /// profile), or if what an earlier profile with the id left behind can't be cleared. The id is
        /// reserved by creating its file exclusively before anything is cleared, and the file is
        /// removed again when a later step fails.
        /// </summary>
        public static bool CreateProfile(string profile, string? copyFromProfile)
        {
            // "default" is the built-in profile: it always exists for the user, even without a file,
            // and must never be created (or purged) through here, in any letter case.
            if (!IsValidProfileName(profile) || profile.Equals(DefaultProfile, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning($"ProfileManager.CreateProfile: invalid name '{profile}'");
                return false;
            }

            string target = ConfigPath(profile);
            bool created = false;
            try
            {
                // Profile ids are file names, so ids differing only in case are the same profile. A
                // registered profile whose file is missing still counts as existing.
                if (File.Exists(target) ||
                    GetProfiles().Any(p => p.Equals(profile, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.LogWarning($"ProfileManager.CreateProfile: '{profile}' already exists");
                    return false;
                }

                AfterCreateExistenceCheckForTests?.Invoke();

                string? copySource = null;
                if (copyFromProfile != null)
                {
                    bool isDefault = copyFromProfile.Equals(DefaultProfile, StringComparison.OrdinalIgnoreCase);
                    if (!isDefault && !IsValidProfileName(copyFromProfile))
                    {
                        Logger.LogWarning($"ProfileManager.CreateProfile: invalid profile to copy '{copyFromProfile}'");
                        return false;
                    }

                    if (File.Exists(ConfigPath(copyFromProfile)))
                    {
                        copySource = ConfigPath(copyFromProfile);
                    }
                    else if (!isDefault)
                    {
                        // Creating an empty profile instead would silently drop the copy asked for.
                        Logger.LogWarning($"ProfileManager.CreateProfile: the profile to copy, '{copyFromProfile}', no longer exists");
                        return false;
                    }
                }

                // Reserve the id first: both branches create the file exclusively, so if another editor
                // created the same profile after the check above, this one fails instead of
                // overwriting it, and before it has purged anything that profile owns.
                if (copySource != null)
                {
                    try
                    {
                        File.Copy(copySource, target, overwrite: false);
                    }
                    catch (IOException ex) when ((ex.HResult & 0xFFFF) is ErrorFileExists or ErrorAlreadyExists)
                    {
                        Logger.LogWarning($"ProfileManager.CreateProfile: '{profile}' was created by another editor");
                        return false;
                    }
                }
                else if (!TryCreateFileExclusive(target, EmptyConfigJson))
                {
                    Logger.LogWarning($"ProfileManager.CreateProfile: '{profile}' was created by another editor");
                    return false;
                }

                created = true;

                if (!PurgeLeftoversOfReusedId(profile))
                {
                    RemoveCreatedProfileFile(target);
                    return false;
                }

                // Register it under the transaction lock, so that this read-modify-write of
                // settings.json cannot interleave with another editor window's delete or switch.
                using (FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock())
                {
                    if (transactionLock == null)
                    {
                        Logger.LogWarning("ProfileManager.CreateProfile: could not acquire the editor transaction lock.");
                        RemoveCreatedProfileFile(target);
                        return false;
                    }

                    JsonObject root = (ReadSettingsRoot() as JsonObject) ?? CreateDefaultSettingsRoot();
                    JsonObject properties = EnsureObject(root, "properties");
                    AddToConfigurationList(properties, profile);
                    WriteSettingsRoot(root);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.CreateProfile('{profile}'): {ex.Message}");
                if (created)
                {
                    RemoveCreatedProfileFile(target);
                }

                return false;
            }
        }

        // A profile that used this id before may have left mapping membership, keyboard assignments or
        // a display name behind (a delete from an older build, or one whose last cleanup steps failed).
        // Clears them, so a new profile with the id starts empty instead of showing the old profile's
        // turned-off rows or being auto-switched to. Call only after the id was reserved by creating
        // its file. Runs under the editor transaction lock, which a running delete holds until it has
        // finished or rolled back.
        private static bool PurgeLeftoversOfReusedId(string profile)
        {
            using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock();
            if (transactionLock == null)
            {
                Logger.LogWarning($"ProfileManager.CreateProfile('{profile}'): could not acquire the editor transaction lock.");
                return false;
            }

            // A moved-aside "{id}.deleting-*.json" means a delete of this id rolled back but could not
            // move its file back (our new file was in the way): that profile still exists, so neither
            // purge its membership nor take its id. Once our file is removed, the leftover cleanup in
            // GetProfiles() puts the original back (the id of such a delete is still registered, or
            // the set-aside name records that it never was).
            if (FindSetAsideFiles().Any(f => f.Id.Equals(profile, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogWarning($"ProfileManager.CreateProfile('{profile}'): a profile with this id is still being deleted or restored.");
                return false;
            }

            if (!SettingsManager.RemoveProfileMembershipWhileLocked(profile, out _) ||
                !DeviceProfileManager.RemoveAssignmentsForProfile(profile) ||
                !ProfileMetadataManager.Remove(profile))
            {
                Logger.LogWarning($"ProfileManager.CreateProfile: could not clear what an earlier '{profile}' left behind");
                return false;
            }

            return true;
        }

        // Undoes the file creation of a CreateProfile that failed after reserving the id.
        private static void RemoveCreatedProfileFile(string path)
        {
            if (!TryDeleteFile(path))
            {
                Logger.LogError($"ProfileManager.CreateProfile: could not remove '{path}' after a failed create.");
            }
        }

        /// <summary>
        /// Deletes a profile's config file and unregisters it, dropping the profile from the editor
        /// settings' membership as well. The built-in "default" profile cannot be deleted. If the
        /// deleted profile was active, switches to "default" and signals a reload.
        /// Returns false when the profile could not be deleted. In that case the profile is normally
        /// kept as it was (its {name}.json, its registration and its mappings' membership); see
        /// <see cref="DeleteProfileWithResult(string)"/> for the rare case where undoing failed too.
        /// </summary>
        public static bool DeleteProfile(string profile) => DeleteProfileWithResult(profile) == ProfileDeleteResult.Deleted;

        /// <summary>
        /// Like <see cref="DeleteProfile"/>, but tells a failure that left the profile exactly as it was
        /// (<see cref="ProfileDeleteResult.Kept"/>) apart from one whose undo also failed
        /// (<see cref="ProfileDeleteResult.NotFullyRestored"/>).
        /// </summary>
        public static ProfileDeleteResult DeleteProfileWithResult(string profile) =>
            DeleteProfileWithResult(profile, DefaultLockTimeoutMS);

        internal static ProfileDeleteResult DeleteProfileWithResult(string profile, int lockTimeoutMS)
        {
            if (string.IsNullOrWhiteSpace(profile) || profile.Equals(DefaultProfile, StringComparison.OrdinalIgnoreCase))
            {
                return ProfileDeleteResult.Kept;
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
                transactionLock = SettingsManager.TryAcquireMappingTransactionLock(lockTimeoutMS);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): {ex.Message}");
                return ProfileDeleteResult.Kept;
            }

            if (transactionLock == null)
            {
                Logger.LogWarning($"ProfileManager.DeleteProfile('{profile}'): could not acquire the editor transaction lock.");
                return ProfileDeleteResult.Kept;
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
                        // An unregistered profile's delete leaves no trace in settings.json, so its
                        // set-aside name records that; see RecoverSetAsideFiles.
                        string marker = IsRegistered(profile) ? string.Empty : "unregistered-";
                        string target = Path.Combine(SettingsDirectory, $"{profile}.deleting-{marker}{Guid.NewGuid():N}.json");
                        File.Move(path, target);
                        setAsidePath = target;
                    }

                    if (!SettingsManager.RemoveProfileMembershipWhileLocked(profile, out previousEditorSettings))
                    {
                        // Keep the profile rather than leave its rows behind: with the file gone, a later
                        // profile reusing the id would show them as its own turned-off rows.
                        Logger.LogWarning($"ProfileManager.DeleteProfile('{profile}'): editor membership cleanup failed; keeping the profile.");
                        return RollBackDelete(profile, path, setAsidePath, previousEditorSettings: null);
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

                    BeforeDeleteCommitForTests?.Invoke();
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

                    return ProfileDeleteResult.Deleted;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): {ex.Message}");
                    if (committed)
                    {
                        return ProfileDeleteResult.Deleted;
                    }

                    // A step before settings.json was updated failed: put the membership and the file
                    // back so the profile stays exactly as it was. The lock is still held here.
                    return RollBackDelete(profile, path, setAsidePath, previousEditorSettings);
                }
            }
        }

        // Undoes the steps of a DeleteProfile that failed before settings.json was updated. The caller
        // still holds the editor transaction lock. Reports NotFullyRestored when an undo step fails
        // too, so the UI does not claim the profile was kept as it was.
        private static ProfileDeleteResult RollBackDelete(string profile, string path, string? setAsidePath, EditorSettings? previousEditorSettings)
        {
            bool restored = true;
            if (previousEditorSettings != null && !SettingsManager.RestoreSettingsWhileLocked(previousEditorSettings))
            {
                Logger.LogError($"ProfileManager.DeleteProfile('{profile}'): could not restore the editor membership.");
                restored = false;
            }

            if (!RestoreSetAsideFile(setAsidePath, path))
            {
                restored = false;
            }

            // While the file was moved aside, the engine may have reloaded (e.g. after a change in
            // Settings) and found no {name}.json for the active profile, which leaves it with an empty
            // remap table. Ask it to reload now that the file is back; an extra reload is harmless.
            if (setAsidePath != null && GetActiveProfile().Equals(profile, StringComparison.OrdinalIgnoreCase))
            {
                SignalEngineReload();
            }

            return restored ? ProfileDeleteResult.Kept : ProfileDeleteResult.NotFullyRestored;
        }

        // Puts a config file moved aside by DeleteProfile back under its own name. If that fails, the
        // moved file is left in place (hidden, but not lost), the failure is logged with its path, and
        // false is returned.
        private static bool RestoreSetAsideFile(string? setAsidePath, string path)
        {
            if (setAsidePath == null)
            {
                return true;
            }

            try
            {
                File.Move(setAsidePath, path);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileManager: could not move '{setAsidePath}' back to '{path}': {ex.Message}");
                return false;
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
                SuppressedEngineReloadsForTests++;
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

        // The ids listed in settings.json's "keyboardConfigurations" (case-insensitive). Throws when
        // settings.json exists but cannot be read.
        private static HashSet<string> ReadRegisteredProfiles()
        {
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ReadSettingsRoot()?["properties"]?["keyboardConfigurations"]?["value"] is JsonArray configs)
            {
                foreach (JsonNode? item in configs)
                {
                    if (item?.GetValue<string>() is string name)
                    {
                        registered.Add(name);
                    }
                }
            }

            return registered;
        }

        // Whether the profile is registered. When that cannot be read, it counts as unregistered,
        // which makes the leftover cleanup keep a moved-aside file rather than remove it.
        private static bool IsRegistered(string profile)
        {
            try
            {
                return ReadRegisteredProfiles().Contains(profile);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileManager: could not read the registered profiles: {ex.Message}");
                return false;
            }
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

        // Writes settings.json through a temporary file that replaces it in one step (as
        // SettingsManager.WriteSettings does for editorSettings.json), so a failed write leaves the old
        // file intact instead of a truncated one. Throws on failure.
        private static void WriteSettingsRoot(JsonNode root)
        {
            Directory.CreateDirectory(SettingsDirectory);
            string temporaryPath = Path.Combine(SettingsDirectory, $"settings.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
                SettingsFileReplacer.Replace(temporaryPath, SettingsJsonPath);
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }

            // A replace by rename raises no last-write change notification for settings.json, and
            // its watchers (this editor's other windows, the Settings app) only listen for those.
            // A reader that has the file open for a moment can block this too, so it is retried
            // briefly; a lasting failure only delays the other windows until the next change.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.SetLastWriteTimeUtc(SettingsJsonPath, DateTime.UtcNow);
                    break;
                }
                catch (Exception) when (attempt < 3)
                {
                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"ProfileManager: could not update the time stamp of settings.json: {ex.Message}");
                    break;
                }
            }
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

            if (!array.Any(n => string.Equals(n?.GetValue<string>(), profile, StringComparison.OrdinalIgnoreCase)))
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
                if (string.Equals(array[i]?.GetValue<string>(), profile, StringComparison.OrdinalIgnoreCase))
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
