// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    internal static class SettingsManager
    {
        internal const string DisableInitializationSwitch = "PowerToys.KeyboardManagerEditorUI.DisableSettingsInitialization";

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static readonly KeyboardMappingService? _mappingService;

        // Resolved on every use (not cached in static fields) so unit tests can redirect the folder
        // through KeyboardManagerPaths before touching any file.
        private static string SettingsDirectory => KeyboardManagerPaths.SettingsDirectory;

        private static string SettingsFilePath => Path.Combine(SettingsDirectory, "editorSettings.json");

        private static string TransactionLockFilePath => Path.Combine(SettingsDirectory, "editorTransaction.lock");

        /// <summary>
        /// Gets a value indicating whether the native C++ wrapper DLL is available.
        /// When false the editor runs in read-only / XAML-preview mode using JSON settings only.
        /// </summary>
        internal static bool IsNativeServiceAvailable => _mappingService is not null;

        /// <summary>
        /// Gets the profile the engine is currently applying, or <see langword="null"/> when the native
        /// service is unavailable (preview mode), which keeps the settings operations profile-agnostic.
        /// </summary>
        /// <remarks>
        /// Read live rather than taken from <see cref="KeyboardMappingService.ConfigurationName"/>: that
        /// property is captured in the service constructor, so it goes stale as soon as the profile is
        /// switched while the editor is open (by the picker, the cycle hotkey, or device auto-switch).
        /// Both come from the same place — "activeConfiguration" in the Keyboard Manager settings.json.
        /// </remarks>
        private static string? CurrentProfileName =>
            _mappingService is null ? null : ProfileManager.GetActiveProfile();

        public static EditorSettings EditorSettings { get; set; }

        /// <summary>
        /// Gets the profile the editor settings were last reconciled against, or null when they
        /// have not been reconciled yet. The rows shown must be read with a mapping service of the
        /// same profile.
        /// </summary>
        internal static string? ReconciledProfile { get; private set; }

        static SettingsManager()
        {
            if (AppContext.TryGetSwitch(DisableInitializationSwitch, out bool disableInitialization) && disableInitialization)
            {
                _mappingService = null;
                EditorSettings = new EditorSettings();
                return;
            }

            using FileStream? transactionLock = TryAcquireMappingTransactionLock();
            if (transactionLock == null)
            {
                ManagedCommon.Logger.LogError("Could not acquire the Keyboard Manager editor transaction lock during startup.");
                _mappingService = null;
                EditorSettings = new EditorSettings();
                return;
            }

            try
            {
                _mappingService = new KeyboardMappingService();
            }
            catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
            {
                ManagedCommon.Logger.LogWarning($"Native KBM library unavailable, running in standalone mode: {ex.Message}");
                _mappingService = null;
            }

            EditorSettings = LoadSettings();
        }

        private static EditorSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    if (_mappingService is not null)
                    {
                        EditorSettings createdSettings = CreateSettingsFromKeyboardManagerService(_mappingService);
                        WriteSettings(createdSettings);
                        return createdSettings;
                    }

                    return new EditorSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
                EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
                NormalizeSettings(settings);
                return settings;
            }
            catch (Exception)
            {
                return new EditorSettings();
            }
        }

        public static bool WriteSettings(EditorSettings editorSettings)
        {
            string? temporaryPath = null;
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);
                temporaryPath = Path.Combine(SettingsDirectory, $"editorSettings.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporaryPath, json);
                SettingsFileReplacer.Replace(temporaryPath, SettingsFilePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        internal static FileStream? TryAcquireMappingTransactionLock(int timeoutMS = 10_000)
        {
            Directory.CreateDirectory(SettingsDirectory);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
            do
            {
                try
                {
                    return new FileStream(
                        TransactionLockFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException exception) when (IsFileLockContention(exception))
                {
                    Thread.Sleep(50);
                }
            }
            while (DateTime.UtcNow < deadline);

            return null;
        }

        /// <summary>What a read of editorSettings.json found.</summary>
        internal enum SettingsReadResult
        {
            /// <summary>The file was read and is now the loaded settings.</summary>
            Loaded,

            /// <summary>There is no file. Nothing can be lost by creating one.</summary>
            Missing,

            /// <summary>
            /// The file exists but could not be read or parsed right now (locked or half-written by
            /// another writer such as a Settings restore, or damaged). The loaded settings are unchanged.
            /// </summary>
            Unreadable,
        }

        internal static bool TryReloadSettings() => TryReloadSettings(out _);

        internal static bool TryReloadSettings(out SettingsReadResult result)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    result = SettingsReadResult.Missing;
                    return false;
                }

                string json = File.ReadAllText(SettingsFilePath);
                EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
                NormalizeSettings(settings);
                EditorSettings = settings;
                result = SettingsReadResult.Loaded;
                return true;
            }
            catch (Exception)
            {
                result = SettingsReadResult.Unreadable;
                return false;
            }
        }

        /// <summary>
        /// Whether the editor settings may be created again from the active profile's native mappings
        /// after a read that did not load them. Doing so loses every turned-off row and every other
        /// profile's rows, which exist only in that file, so it is allowed for a file that is not
        /// there, and for an unreadable one only where the caller says so (startup, after retries).
        /// A live reload never allows it: it keeps the last good state and tries again.
        /// </summary>
        internal static bool ShouldRegenerateSettings(SettingsReadResult read, bool regenerateUnreadableSettings) =>
            read == SettingsReadResult.Missing || (read == SettingsReadResult.Unreadable && regenerateUnreadableSettings);

        internal static bool HasSettingsChangedOnDisk() =>
            !File.Exists(SettingsFilePath) || DiffersFromLoadedSettings(File.ReadAllText(SettingsFilePath));

        internal static bool DiffersFromLoadedSettings(string json)
        {
            EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
            NormalizeSettings(settings);

            // Normalize both sides: the editor's own edits can leave index order that normalization rewrites.
            return JsonSerializer.Serialize(settings, _jsonOptions) != JsonSerializer.Serialize(CloneSettings(), _jsonOptions);
        }

        private static bool IsFileLockContention(IOException exception)
        {
            int errorCode = exception.HResult & 0xFFFF;
            return errorCode is 32 or 33;
        }

        /// <summary>
        /// Loads the profile that is active now, for a live reload — after
        /// <see cref="ProfileManager.SetActiveProfile"/>, or after the engine switched profiles on its
        /// own (device auto-switch, cycle hotkey). Under the transaction lock it reads that profile's
        /// native configuration into a new service and reconciles the editor settings against it,
        /// which tags that profile's mappings and leaves the other profiles' entries inactive.
        /// Returns the new service (the caller owns it) only when both succeeded. Otherwise it returns
        /// null and nothing has changed: in particular an editorSettings.json that cannot be read
        /// right now is not rebuilt from the active mappings, so the caller keeps showing its last
        /// good state and tries again. Null as well when the native service is unavailable (preview mode).
        /// A caller that tries again on a timer passes <paramref name="waitForLock"/> = false: the
        /// lock is then tried once, so a lock that another editor process keeps holding does not
        /// block the UI thread at every try.
        /// </summary>
        internal static KeyboardMappingService? TryLoadActiveProfile(bool waitForLock = true)
        {
            if (_mappingService is null)
            {
                return null;
            }

            using FileStream? transactionLock = waitForLock ? TryAcquireMappingTransactionLock() : TryAcquireMappingTransactionLock(timeoutMS: 0);
            if (transactionLock == null)
            {
                if (waitForLock)
                {
                    ManagedCommon.Logger.LogError("Could not acquire the Keyboard Manager editor transaction lock while loading the active profile.");
                }

                return null;
            }

            KeyboardMappingService? service = null;
            try
            {
                service = new KeyboardMappingService();
                if (ReconcileFromDisk(GetActiveMappings(service), service.ConfigurationName, regenerateUnreadableSettings: false))
                {
                    KeyboardMappingService loaded = service;
                    service = null;
                    return loaded;
                }

                return null;
            }
            catch (Exception exception)
            {
                ManagedCommon.Logger.LogError("Failed to load the active Keyboard Manager profile: " + exception.Message);
                return null;
            }
            finally
            {
                service?.Dispose();
            }
        }

        /// <summary>
        /// Drops <paramref name="profileName"/> from every mapping's profile membership, so deleting a
        /// profile leaves no dangling references behind. A mapping that belonged to this profile and to
        /// no other is deleted with it; one that is still named by another profile is kept and stays in
        /// that profile. Mappings carrying no profile at all are left alone — an untagged mapping
        /// predates profiles and is treated as belonging everywhere, so a deletion must not adopt it.
        /// </summary>
        public static bool RemoveProfileMembership(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return false;
            }

            try
            {
                // Run under the same transaction lock + in-lock reload as ordinary mapping edits
                // (add / delete / toggle). Without it, cloning the process-local EditorSettings and
                // replacing the whole file would clobber a concurrent editor process's in-flight
                // change — e.g. a mapping just added and turned off in another window — because this
                // write never saw it. Reloading inside the lock makes the removal a read-modify-write
                // against the latest on-disk state.
                using FileStream? transactionLock = TryAcquireMappingTransactionLock();
                if (transactionLock == null)
                {
                    ManagedCommon.Logger.LogError($"SettingsManager.RemoveProfileMembership('{profileName}'): could not acquire the transaction lock.");
                    return false;
                }

                return RemoveProfileMembershipWhileLocked(profileName, out _);
            }
            catch (Exception exception)
            {
                ManagedCommon.Logger.LogError($"SettingsManager.RemoveProfileMembership('{profileName}'): {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// The part of <see cref="RemoveProfileMembership"/> that runs once the caller already holds
        /// the transaction lock (<see cref="TryAcquireMappingTransactionLock"/>): reloads the latest
        /// settings, drops the profile's membership and writes the result. <paramref name="previous"/>
        /// receives the settings as they were before the change when something was written, so a
        /// caller that fails later in the same transaction can put them back with
        /// <see cref="RestoreSettingsWhileLocked"/>. With no editor settings file yet, nothing can
        /// reference the profile and the call succeeds without writing.
        /// </summary>
        internal static bool RemoveProfileMembershipWhileLocked(string profileName, out EditorSettings? previous)
        {
            previous = null;
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return true;
                }

                if (!TryReloadSettings())
                {
                    ManagedCommon.Logger.LogError($"SettingsManager.RemoveProfileMembership('{profileName}'): could not reload the latest settings.");
                    return false;
                }

                EditorSettings updatedSettings = CloneSettings();
                if (!ApplyProfileMembershipRemoval(updatedSettings, profileName))
                {
                    return true; // nothing referenced the profile; a no-op is still success
                }

                if (!WriteSettings(updatedSettings))
                {
                    ManagedCommon.Logger.LogError($"SettingsManager.RemoveProfileMembership('{profileName}'): could not write the settings.");
                    return false;
                }

                previous = EditorSettings;
                EditorSettings = updatedSettings;
                return true;
            }
            catch (Exception exception)
            {
                ManagedCommon.Logger.LogError($"SettingsManager.RemoveProfileMembership('{profileName}'): {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Writes back settings captured by <see cref="RemoveProfileMembershipWhileLocked"/>. The
        /// caller must still hold the transaction lock it held for that call, so nothing else can
        /// have changed the file in between.
        /// </summary>
        internal static bool RestoreSettingsWhileLocked(EditorSettings previous)
        {
            if (!WriteSettings(previous))
            {
                return false;
            }

            EditorSettings = previous;
            return true;
        }

        /// <summary>
        /// Pure core of <see cref="RemoveProfileMembership"/>: mutates <paramref name="settings"/> in
        /// place and returns whether anything changed. Split out from the public method (which also
        /// writes to disk) so the membership rules are unit-testable in isolation. A mapping owned
        /// only by <paramref name="profileName"/> is removed from both indexes; one still named by
        /// another profile is kept with the remaining membership; an untagged (profile-less) mapping
        /// is left untouched; the profile index is rebuilt and a matching active profile cleared.
        /// </summary>
        internal static bool ApplyProfileMembershipRemoval(EditorSettings settings, string profileName)
        {
            bool changed = false;
            var orphanedIds = new List<string>();

            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                if (!RemoveProfile(entry.Value, profileName))
                {
                    continue;
                }

                changed = true;
                if (GetProfiles(entry.Value).Count == 0)
                {
                    orphanedIds.Add(entry.Key);
                }
            }

            foreach (string id in orphanedIds)
            {
                ShortcutOperationType operationType = settings.ShortcutSettingsDictionary[id].Shortcut.OperationType;
                settings.ShortcutSettingsDictionary.Remove(id);
                if (settings.ShortcutsByOperationType.TryGetValue(operationType, out List<string>? operationIds))
                {
                    operationIds.Remove(id);
                }
            }

            foreach (string profile in settings.ProfileDictionary.Keys
                .Where(profile => profile.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                changed |= settings.ProfileDictionary.Remove(profile);
            }

            if (!changed)
            {
                return false;
            }

            settings.ProfileDictionary = BuildProfileIndex(settings);
            if (string.Equals(settings.ActiveProfile, profileName, StringComparison.OrdinalIgnoreCase))
            {
                settings.ActiveProfile = string.Empty;
            }

            return true;
        }

        private static EditorSettings CreateSettingsFromKeyboardManagerService(KeyboardMappingService service) =>
            CreateSettingsFromMappings(GetActiveMappings(service), service.ConfigurationName);

        // Every row created here is tagged with the given profile, so there is nothing to migrate.
        private static EditorSettings CreateSettingsFromMappings(IEnumerable<ShortcutKeyMapping> activeMappings, string? profileName)
        {
            EditorSettings settings = new EditorSettings
            {
                ActiveProfile = profileName ?? string.Empty,
                SchemaVersion = string.IsNullOrWhiteSpace(profileName) ? 0 : EditorSettings.CurrentSchemaVersion,
            };

            foreach (ShortcutKeyMapping mapping in activeMappings)
            {
                AddShortcutMapping(settings, mapping, settings.ActiveProfile);
            }

            return settings;
        }

        // The native mappings of the profile a service has loaded, in the editor's own form: shortcut
        // mappings (RunProgram, OpenUri, RemapShortcut, RemapText), then single key to key, then
        // single key to text.
        private static List<ShortcutKeyMapping> GetActiveMappings(KeyboardMappingService service)
        {
            List<ShortcutKeyMapping> activeMappings = service.GetShortcutMappings();
            activeMappings.AddRange(service.GetSingleKeyMappings().Select(mapping => new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = mapping.TargetKey,

                // GetSingleKeyMappings surfaces both the regular and the "alone" (dual-key) tables,
                // tagging the latter with IsAlone; preserve that as the per-entry Condition so an
                // alone remap round-trips instead of loading back as an unconditional (Always) one.
                Condition = mapping.IsAlone ? SingleKeyRemapCondition.Alone : SingleKeyRemapCondition.Always,
            }));
            activeMappings.AddRange(service.GetKeyToTextMappings().Select(mapping => new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapText,
                OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                TargetText = mapping.TargetText,
            }));
            return activeMappings;
        }

        /// <summary>
        /// Reconciles the editor metadata with the native mappings.
        /// </summary>
        /// <param name="regenerateUnreadableSettings">
        /// Whether to rebuild unreadable metadata from the active native mappings. Pass false when the file
        /// may be mid-write (e.g. during a Settings restore) so a failed read does not drop disabled rows.
        /// When true (startup), the read is first tried a few times, since rebuilding loses those rows.
        /// </param>
        /// <returns>Whether the loaded metadata is now current.</returns>
        public static bool CorrelateServiceAndEditorMappings(bool regenerateUnreadableSettings = true)
        {
            if (_mappingService is null)
            {
                return false;
            }

            using FileStream? transactionLock = TryAcquireMappingTransactionLock();
            if (transactionLock == null)
            {
                ManagedCommon.Logger.LogError("Could not acquire the Keyboard Manager editor transaction lock during reconciliation.");
                return false;
            }

            try
            {
                using var service = new KeyboardMappingService();
                List<ShortcutKeyMapping> activeMappings = GetActiveMappings(service);
                if (regenerateUnreadableSettings)
                {
                    // An editorSettings.json that exists but cannot be read may only be held by
                    // another writer for a moment: try for about half a second before rebuilding it.
                    const int Attempts = 5;
                    const int RetryDelayMS = 100;
                    for (int attempt = 1; attempt < Attempts; attempt++)
                    {
                        if (ReconcileFromDisk(activeMappings, service.ConfigurationName, regenerateUnreadableSettings: false))
                        {
                            return true;
                        }

                        Thread.Sleep(RetryDelayMS);
                    }
                }

                return ReconcileFromDisk(activeMappings, service.ConfigurationName, regenerateUnreadableSettings);
            }
            catch (Exception exception)
            {
                ManagedCommon.Logger.LogError("Failed to reconcile Keyboard Manager editor settings: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// The part of a reconciliation after the native mappings have been read (the caller holds the
        /// transaction lock): reads editorSettings.json and reconciles it against
        /// <paramref name="activeMappings"/> of <paramref name="profileName"/>. When the file could not
        /// be loaded it is created again from those mappings only where
        /// <see cref="ShouldRegenerateSettings"/> allows; otherwise this returns false, and neither
        /// the file nor the loaded settings have changed.
        /// </summary>
        internal static bool ReconcileFromDisk(IReadOnlyList<ShortcutKeyMapping> activeMappings, string? profileName, bool regenerateUnreadableSettings)
        {
            if (!ReconcileFromDiskCore(activeMappings, profileName, regenerateUnreadableSettings))
            {
                return false;
            }

            ReconciledProfile = profileName;
            return true;
        }

        private static bool ReconcileFromDiskCore(IReadOnlyList<ShortcutKeyMapping> activeMappings, string? profileName, bool regenerateUnreadableSettings)
        {
            if (!TryReloadSettings(out SettingsReadResult read))
            {
                if (!ShouldRegenerateSettings(read, regenerateUnreadableSettings))
                {
                    return false;
                }

                EditorSettings createdSettings = CreateSettingsFromMappings(activeMappings, profileName);
                if (!WriteSettings(createdSettings))
                {
                    return false;
                }

                EditorSettings = createdSettings;
                return true;
            }

            return ReconcileLoadedSettings(activeMappings, profileName);
        }

        /// <summary>
        /// The part of <see cref="ReconcileFromDisk"/> after the native mappings and
        /// the editor settings have been loaded (the caller holds the transaction lock): reconciles
        /// the loaded settings against <paramref name="activeMappings"/> of
        /// <paramref name="profileName"/>, migrating a file from the single-profile editor on the
        /// way, and writes the result when anything changed.
        /// </summary>
        internal static bool ReconcileLoadedSettings(IEnumerable<ShortcutKeyMapping> activeMappings, string? profileName)
        {
            EditorSettings updatedSettings = CloneSettings(EditorSettings);
            if (!ReconcileMappings(updatedSettings, activeMappings, profileName, ExistingProfiles))
            {
                return true;
            }

            if (!WriteSettings(updatedSettings))
            {
                return false;
            }

            EditorSettings = updatedSettings;
            return true;
        }

        internal static bool ReconcileMappings(
            EditorSettings settings,
            IEnumerable<ShortcutKeyMapping> activeMappings,
            string? profileName = null,
            Func<IEnumerable<string>>? existingProfiles = null)
        {
            bool profileAware = !string.IsNullOrWhiteSpace(profileName);
            bool shortcutSettingsChanged = NormalizeSettings(settings);

            // Before the rows are matched below, so a migrated row is matched with its profiles.
            if (profileAware && MigrateLegacyRows(settings, existingProfiles))
            {
                shortcutSettingsChanged = true;
            }

            if (profileAware && !string.Equals(settings.ActiveProfile, profileName, StringComparison.OrdinalIgnoreCase))
            {
                settings.ActiveProfile = profileName!;
                shortcutSettingsChanged = true;
            }

            var matchedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ShortcutKeyMapping mapping in activeMappings)
            {
                KeyValuePair<string, ShortcutSettings> existingEntry = settings.ShortcutSettingsDictionary
                    .FirstOrDefault(entry =>
                        !matchedIds.Contains(entry.Key) &&
                        (!profileAware || BelongsToProfile(entry.Value, profileName!) || GetProfiles(entry.Value).Count == 0) &&
                        MappingsEquivalent(entry.Value.Shortcut, mapping));
                if (string.IsNullOrEmpty(existingEntry.Key))
                {
                    existingEntry = settings.ShortcutSettingsDictionary.FirstOrDefault(entry =>
                        !matchedIds.Contains(entry.Key) &&
                        (!profileAware || BelongsToProfile(entry.Value, profileName!) || GetProfiles(entry.Value).Count == 0) &&
                        HasSameTriggerAndScope(entry.Value.Shortcut, mapping));
                }

                if (!string.IsNullOrEmpty(existingEntry.Key))
                {
                    if (profileAware && !BelongsToProfile(existingEntry.Value, profileName!))
                    {
                        AddProfile(existingEntry.Value, profileName!);
                        shortcutSettingsChanged = true;
                    }

                    if (!MappingsEquivalent(existingEntry.Value.Shortcut, mapping))
                    {
                        if (profileAware && GetProfiles(existingEntry.Value).Count > 1)
                        {
                            RemoveProfile(existingEntry.Value, profileName!);
                            matchedIds.Add(AddShortcutMapping(settings, mapping, profileName));
                            shortcutSettingsChanged = true;
                            continue;
                        }

                        TryApplyShortcutKeyMapping(settings, mapping, existingEntry.Key, profileName);
                        shortcutSettingsChanged = true;
                    }
                    else if (!existingEntry.Value.IsActive)
                    {
                        existingEntry.Value.IsActive = true;
                        shortcutSettingsChanged = true;
                    }

                    if (existingEntry.Value.Id != existingEntry.Key)
                    {
                        existingEntry.Value.Id = existingEntry.Key;
                        shortcutSettingsChanged = true;
                    }

                    matchedIds.Add(existingEntry.Key);
                }
                else
                {
                    matchedIds.Add(AddShortcutMapping(settings, mapping, profileName));
                    shortcutSettingsChanged = true;
                }
            }

            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                bool shouldBeActive = matchedIds.Contains(entry.Key);
                if (entry.Value.IsActive != shouldBeActive)
                {
                    entry.Value.IsActive = shouldBeActive;
                    shortcutSettingsChanged = true;
                }
            }

            Dictionary<ShortcutOperationType, List<string>> expectedIndex = settings.ShortcutSettingsDictionary
                .GroupBy(entry => entry.Value.Shortcut.OperationType)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.Key).ToList());
            if (!OperationIndexesEqual(settings.ShortcutsByOperationType, expectedIndex))
            {
                settings.ShortcutsByOperationType = expectedIndex;
                shortcutSettingsChanged = true;
            }

            Dictionary<string, List<string>> expectedProfileIndex = BuildProfileIndex(settings);
            if (!ProfileIndexesEqual(settings.ProfileDictionary, expectedProfileIndex))
            {
                settings.ProfileDictionary = expectedProfileIndex;
                shortcutSettingsChanged = true;
            }

            return shortcutSettingsChanged;
        }

        /// <summary>
        /// One-time upgrade of a file from the single-profile editor (v0.100 to v0.102), marked by
        /// <see cref="EditorSettings.SchemaVersion"/> below <see cref="EditorSettings.CurrentSchemaVersion"/>.
        /// That editor stored turned-off rows without a profile and deleted their native mappings, so
        /// reconciling would never tag them. Untagged rows are treated as belonging to every profile,
        /// so once profiles can be created such a row would show up in a new empty profile and could
        /// be deleted or claimed from there, taking it away from the profile it came from.
        /// Where an untagged row goes depends only on the rows of the file:
        /// <list type="bullet">
        /// <item>No row names a profile other than "default": the file comes from a released
        /// single-profile editor (v0.101 and v0.102 tag rows with "default" only), which could only
        /// use "default", so every row goes to "default".</item>
        /// <item>Some row names another profile: a pre-release profile-aware build wrote the file, and it
        /// showed an untagged row in every profile. The row is tagged with every profile that
        /// exists now (<paramref name="existingProfiles"/>), so it stays where it was visible,
        /// and deleting it from one profile no longer deletes it from the others.</item>
        /// </list>
        /// The recorded <see cref="EditorSettings.ActiveProfile"/> and the profile active now play
        /// no part, so it does not matter when the migration runs (the first start after the
        /// upgrade, after a switch, after a restored backup brings back an old file) or which
        /// profile an earlier write recorded.
        /// </summary>
        /// <param name="settings">The settings to migrate in place.</param>
        /// <param name="existingProfiles">The profiles that exist now; when null, "default" and
        /// the profiles the file's rows name.</param>
        /// <returns>Whether the settings changed.</returns>
        internal static bool MigrateLegacyRows(EditorSettings settings, Func<IEnumerable<string>>? existingProfiles)
        {
            if (settings.SchemaVersion >= EditorSettings.CurrentSchemaVersion)
            {
                return false;
            }

            List<string> namedProfiles = settings.ShortcutSettingsDictionary.Values
                .SelectMany(GetProfiles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var targets = new List<string> { ProfileManager.DefaultProfile };
            if (namedProfiles.Any(profile => !profile.Equals(ProfileManager.DefaultProfile, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (string profile in existingProfiles?.Invoke() ?? namedProfiles)
                {
                    if (!string.IsNullOrWhiteSpace(profile) && !targets.Contains(profile, StringComparer.OrdinalIgnoreCase))
                    {
                        targets.Add(profile);
                    }
                }
            }

            foreach (ShortcutSettings shortcutSettings in settings.ShortcutSettingsDictionary.Values)
            {
                if (GetProfiles(shortcutSettings).Count == 0)
                {
                    foreach (string target in targets)
                    {
                        AddProfile(shortcutSettings, target);
                    }
                }
            }

            settings.SchemaVersion = EditorSettings.CurrentSchemaVersion;
            settings.ProfileDictionary = BuildProfileIndex(settings);
            return true;
        }

        // The profiles registered in settings.json or with a config file. Used by the migration
        // to tag the untagged rows of a file from a pre-release profile-aware build.
        private static IEnumerable<string> ExistingProfiles() => ProfileManager.GetProfiles();

        // A profile-aware write tags the row it touches. On a file that no reconcile has migrated
        // yet, that tag would make a released single-profile file look like one from a
        // profile-aware build, so the file is migrated first.
        private static void MigrateBeforeWrite(EditorSettings settings, string? profileName)
        {
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                MigrateLegacyRows(settings, ExistingProfiles);
            }
        }

        // profileName is required on the three writes below, and none of them reads the live active
        // profile: the caller passes the profile it captured (the one its service was built against,
        // or the one the editor is showing), so an engine auto-switch in between cannot move the
        // change to another profile. Null means "no profile" (preview mode) and is profile-agnostic.
        public static bool TryCommitShortcutKeyMapping(ShortcutKeyMapping shortcutKeyMapping, string? replacingId, string? profileName)
        {
            try
            {
                EditorSettings updatedSettings = CloneSettings();
                MigrateBeforeWrite(updatedSettings, profileName);

                // Attribute the mapping to the profile the caller captured when it built the edit
                // (candidateService.ConfigurationName), not the live global: the native save happens
                // first, and if the engine auto-switches in between, re-reading CurrentProfileName here
                // would record the mapping under the newly-active profile even though it landed in the
                // original profile's config.
                if (!TryApplyShortcutKeyMapping(updatedSettings, shortcutKeyMapping, replacingId, profileName))
                {
                    return false;
                }

                if (!WriteSettings(updatedSettings))
                {
                    return false;
                }

                EditorSettings = updatedSettings;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool TryApplyShortcutKeyMapping(
            EditorSettings settings,
            ShortcutKeyMapping shortcutKeyMapping,
            string? replacingId,
            string? profileName = null)
        {
            if (string.IsNullOrEmpty(replacingId))
            {
                AddShortcutMapping(settings, shortcutKeyMapping, profileName);
                return true;
            }

            if (!settings.ShortcutSettingsDictionary.TryGetValue(replacingId, out ShortcutSettings? existingSettings))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                List<string> profiles = GetProfiles(existingSettings);
                if (profiles.Count > 0 && !BelongsToProfile(existingSettings, profileName))
                {
                    return false;
                }

                if (profiles.Count > 1)
                {
                    RemoveProfile(existingSettings, profileName);
                    existingSettings.IsActive = false;
                    AddShortcutMapping(settings, shortcutKeyMapping, profileName);
                    settings.ProfileDictionary = BuildProfileIndex(settings);
                    return true;
                }

                AddProfile(existingSettings, profileName);
                settings.ActiveProfile = profileName;
            }

            ShortcutOperationType previousOperationType = existingSettings.Shortcut.OperationType;
            if (settings.ShortcutsByOperationType.TryGetValue(previousOperationType, out List<string>? previousIds))
            {
                previousIds.Remove(replacingId);
            }

            existingSettings.Shortcut = shortcutKeyMapping;
            existingSettings.IsActive = true;

            if (!settings.ShortcutsByOperationType.TryGetValue(shortcutKeyMapping.OperationType, out List<string>? replacementIds))
            {
                replacementIds = new List<string>();
                settings.ShortcutsByOperationType[shortcutKeyMapping.OperationType] = replacementIds;
            }

            if (!replacementIds.Contains(replacingId))
            {
                replacementIds.Add(replacingId);
            }

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                settings.ProfileDictionary = BuildProfileIndex(settings);
            }

            return true;
        }

        // profileName: the profile captured by the caller (see TryCommitShortcutKeyMapping). A
        // turned-off row is removed without a native save, so its caller passes the profile the
        // editor is showing.
        public static bool TryRemoveShortcutKeyMapping(string guid, string? profileName)
        {
            try
            {
                EditorSettings updatedSettings = CloneSettings();
                MigrateBeforeWrite(updatedSettings, profileName);
                if (!TryApplyShortcutKeyMappingRemoval(updatedSettings, guid, profileName) ||
                    !WriteSettings(updatedSettings))
                {
                    return false;
                }

                EditorSettings = updatedSettings;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool TryApplyShortcutKeyMappingRemoval(EditorSettings settings, string guid, string? profileName = null)
        {
            NormalizeSettings(settings);
            if (!settings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(profileName) && GetProfiles(shortcutSettings).Count > 0)
            {
                if (!BelongsToProfile(shortcutSettings, profileName))
                {
                    return false;
                }

                if (GetProfiles(shortcutSettings).Count > 1)
                {
                    RemoveProfile(shortcutSettings, profileName);
                    shortcutSettings.IsActive = false;
                    settings.ProfileDictionary = BuildProfileIndex(settings);
                    return true;
                }
            }

            ShortcutOperationType operationType = shortcutSettings.Shortcut.OperationType;
            settings.ShortcutSettingsDictionary.Remove(guid);

            if (settings.ShortcutsByOperationType.TryGetValue(operationType, out var value))
            {
                value.Remove(guid);
            }

            settings.ProfileDictionary = BuildProfileIndex(settings);
            return true;
        }

        // profileName: the profile captured by the caller's transaction (see TryCommitShortcutKeyMapping).
        public static bool TrySetShortcutKeyMappingActiveState(string guid, bool isActive, string? profileName)
        {
            try
            {
                EditorSettings updatedSettings = CloneSettings();
                MigrateBeforeWrite(updatedSettings, profileName);
                if (!TryApplyShortcutKeyMappingActiveState(
                        updatedSettings,
                        guid,
                        isActive,
                        profileName) ||
                    !WriteSettings(updatedSettings))
                {
                    return false;
                }

                EditorSettings = updatedSettings;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool TryApplyShortcutKeyMappingActiveState(
            EditorSettings settings,
            string guid,
            bool isActive,
            string? profileName = null)
        {
            NormalizeSettings(settings);
            if (!settings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                if (GetProfiles(shortcutSettings).Count > 0 && !BelongsToProfile(shortcutSettings, profileName))
                {
                    return false;
                }

                AddProfile(shortcutSettings, profileName);
                settings.ActiveProfile = profileName;
                settings.ProfileDictionary = BuildProfileIndex(settings);
            }

            shortcutSettings.IsActive = isActive;
            return true;
        }

        /// <summary>
        /// Resolves the profile used to filter the list. This reads the active profile from disk
        /// (via <see cref="CurrentProfileName"/>), so a caller filtering a whole list should call
        /// it ONCE before the loop and pass the result to
        /// <see cref="IsMappingInActiveProfile(ShortcutSettings, string?)"/> — otherwise the disk
        /// read/parse repeats for every mapping (dozens or hundreds on a large configuration).
        /// </summary>
        internal static string? ResolveActiveProfileForFiltering()
        {
            string? profileName = CurrentProfileName;
            return string.IsNullOrWhiteSpace(profileName) ? EditorSettings.ActiveProfile : profileName;
        }

        internal static bool IsMappingInActiveProfile(ShortcutSettings shortcutSettings, string? activeProfileName)
        {
            return string.IsNullOrWhiteSpace(activeProfileName) ||
                   GetProfiles(shortcutSettings).Count == 0 ||
                   BelongsToProfile(shortcutSettings, activeProfileName);
        }

        // Convenience for single-item checks; resolves the active profile itself. Do not call this
        // in a per-mapping loop — use the overload above with a pre-resolved profile name.
        internal static bool IsMappingInActiveProfile(ShortcutSettings shortcutSettings)
            => IsMappingInActiveProfile(shortcutSettings, ResolveActiveProfileForFiltering());

        internal static bool NormalizeSettings(EditorSettings settings)
        {
            bool changed = false;
            if (settings.ShortcutSettingsDictionary is null)
            {
                settings.ShortcutSettingsDictionary = new Dictionary<string, ShortcutSettings>();
                changed = true;
            }

            if (settings.ProfileDictionary is null)
            {
                settings.ProfileDictionary = new Dictionary<string, List<string>>();
                changed = true;
            }

            if (settings.ShortcutsByOperationType is null)
            {
                settings.ShortcutsByOperationType = new Dictionary<ShortcutOperationType, List<string>>();
                changed = true;
            }

            if (settings.ActiveProfile is null)
            {
                settings.ActiveProfile = string.Empty;
                changed = true;
            }

            var normalizedMappings = new Dictionary<string, ShortcutSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) ||
                    entry.Value is null ||
                    entry.Value.Shortcut is null ||
                    string.IsNullOrWhiteSpace(entry.Value.Shortcut.OriginalKeys) ||
                    normalizedMappings.ContainsKey(entry.Key))
                {
                    changed = true;
                    continue;
                }

                ShortcutSettings shortcutSettings = entry.Value;
                if (!string.Equals(shortcutSettings.Id, entry.Key, StringComparison.Ordinal))
                {
                    shortcutSettings.Id = entry.Key;
                    changed = true;
                }

                List<string> normalizedProfiles = GetProfiles(shortcutSettings)
                    .Where(profile => !string.IsNullOrWhiteSpace(profile))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (!GetProfiles(shortcutSettings).SequenceEqual(normalizedProfiles, StringComparer.OrdinalIgnoreCase))
                {
                    shortcutSettings.Profiles = normalizedProfiles;
                    changed = true;
                }

                changed |= NormalizeMappingStrings(shortcutSettings.Shortcut);
                normalizedMappings.Add(entry.Key, shortcutSettings);
            }

            settings.ShortcutSettingsDictionary = normalizedMappings;

            changed |= MergeProfileDictionaryMembership(settings);

            Dictionary<ShortcutOperationType, List<string>> expectedOperationIndex = settings.ShortcutSettingsDictionary
                .GroupBy(entry => entry.Value.Shortcut.OperationType)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.Key).ToList());
            if (!OperationIndexesEqual(settings.ShortcutsByOperationType, expectedOperationIndex))
            {
                settings.ShortcutsByOperationType = expectedOperationIndex;
                changed = true;
            }

            Dictionary<string, List<string>> expectedProfileIndex = BuildProfileIndex(settings);
            if (!ProfileIndexesEqual(settings.ProfileDictionary, expectedProfileIndex))
            {
                settings.ProfileDictionary = expectedProfileIndex;
                changed = true;
            }

            return changed;
        }

        private static EditorSettings CloneSettings()
        {
            return CloneSettings(EditorSettings);
        }

        private static EditorSettings CloneSettings(EditorSettings source)
        {
            string json = JsonSerializer.Serialize(source, _jsonOptions);
            EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
            NormalizeSettings(settings);
            return settings;
        }

        private static bool NormalizeMappingStrings(ShortcutKeyMapping mapping)
        {
            bool changed = false;
            changed |= SetEmptyIfNull(mapping.TargetKeys, value => mapping.TargetKeys = value);
            changed |= SetEmptyIfNull(mapping.TargetApp, value => mapping.TargetApp = value);
            changed |= SetEmptyIfNull(mapping.TargetText, value => mapping.TargetText = value);
            changed |= SetEmptyIfNull(mapping.ProgramPath, value => mapping.ProgramPath = value);
            changed |= SetEmptyIfNull(mapping.ProgramArgs, value => mapping.ProgramArgs = value);
            changed |= SetEmptyIfNull(mapping.StartInDirectory, value => mapping.StartInDirectory = value);
            changed |= SetEmptyIfNull(mapping.UriToOpen, value => mapping.UriToOpen = value);
            return changed;
        }

        private static bool SetEmptyIfNull(string? value, Action<string> assign)
        {
            if (value is not null)
            {
                return false;
            }

            assign(string.Empty);
            return true;
        }

        private static string AddShortcutMapping(EditorSettings settings, ShortcutKeyMapping mapping, string? profileName = null)
        {
            string guid = Guid.NewGuid().ToString();
            var shortcutSettings = new ShortcutSettings
            {
                Id = guid,
                Shortcut = mapping,
                IsActive = true,
            };

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                shortcutSettings.Profiles.Add(profileName);
                settings.ActiveProfile = profileName;
            }

            settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

            if (!settings.ShortcutsByOperationType.TryGetValue(mapping.OperationType, out System.Collections.Generic.List<string>? value))
            {
                value = new System.Collections.Generic.List<string>();
                settings.ShortcutsByOperationType[mapping.OperationType] = value;
            }

            value.Add(guid);
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                settings.ProfileDictionary = BuildProfileIndex(settings);
            }

            return guid;
        }

        private static List<string> GetProfiles(ShortcutSettings shortcutSettings) =>
            shortcutSettings.Profiles ??= new List<string>();

        private static bool BelongsToProfile(ShortcutSettings shortcutSettings, string profileName) =>
            GetProfiles(shortcutSettings).Contains(profileName, StringComparer.OrdinalIgnoreCase);

        private static bool AddProfile(ShortcutSettings shortcutSettings, string profileName)
        {
            if (BelongsToProfile(shortcutSettings, profileName))
            {
                return false;
            }

            GetProfiles(shortcutSettings).Add(profileName);
            return true;
        }

        private static bool RemoveProfile(ShortcutSettings shortcutSettings, string profileName) =>
            GetProfiles(shortcutSettings).RemoveAll(profile =>
                profile.Equals(profileName, StringComparison.OrdinalIgnoreCase)) > 0;

        private static bool MergeProfileDictionaryMembership(EditorSettings settings)
        {
            settings.ProfileDictionary ??= new Dictionary<string, List<string>>();
            bool changed = false;
            foreach (KeyValuePair<string, List<string>> profile in settings.ProfileDictionary)
            {
                if (string.IsNullOrWhiteSpace(profile.Key))
                {
                    changed = true;
                    continue;
                }

                foreach (string id in (profile.Value ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (settings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings))
                    {
                        changed |= AddProfile(shortcutSettings, profile.Key);
                    }
                }
            }

            return changed;
        }

        private static Dictionary<string, List<string>> BuildProfileIndex(EditorSettings settings)
        {
            var profileIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                foreach (string profileName in GetProfiles(entry.Value).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(profileName))
                    {
                        continue;
                    }

                    if (!profileIndex.TryGetValue(profileName, out List<string>? ids))
                    {
                        ids = new List<string>();
                        profileIndex[profileName] = ids;
                    }

                    ids.Add(entry.Key);
                }
            }

            return profileIndex;
        }

        private static bool HasSameTriggerAndScope(ShortcutKeyMapping first, ShortcutKeyMapping second) =>
            first.OriginalKeys == second.OriginalKeys &&
            string.Equals(first.TargetApp ?? string.Empty, second.TargetApp ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&

            // A single-key remap and its "alone" (tap) counterpart live in separate engine tables and
            // are distinct mappings, so the correlation key must include Condition; otherwise reconcile
            // would collapse an Always and an Alone remap of the same key into one entry.
            first.Condition == second.Condition;

        private static bool MappingsEquivalent(ShortcutKeyMapping first, ShortcutKeyMapping second) =>
            HasSameTriggerAndScope(first, second) &&
            first.TargetKeys == second.TargetKeys &&
            first.OperationType == second.OperationType &&
            first.ExactMatch == second.ExactMatch &&
            first.TargetText == second.TargetText &&
            first.ProgramPath == second.ProgramPath &&
            first.ProgramArgs == second.ProgramArgs &&
            first.StartInDirectory == second.StartInDirectory &&
            first.Elevation == second.Elevation &&
            first.IfRunningAction == second.IfRunningAction &&
            first.Visibility == second.Visibility &&
            first.UriToOpen == second.UriToOpen;

        private static bool OperationIndexesEqual(
            Dictionary<ShortcutOperationType, List<string>> first,
            Dictionary<ShortcutOperationType, List<string>> second) =>
            first.Count == second.Count &&
            first.All(entry =>
                entry.Value is not null &&
                second.TryGetValue(entry.Key, out List<string>? secondIds) &&
                entry.Value.SequenceEqual(secondIds, StringComparer.OrdinalIgnoreCase));

        private static bool ProfileIndexesEqual(
            Dictionary<string, List<string>> first,
            Dictionary<string, List<string>> second) =>
            first.Count == second.Count &&
            first.All(entry =>
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null)
                {
                    return false;
                }

                KeyValuePair<string, List<string>> secondEntry = second.FirstOrDefault(candidate =>
                    candidate.Key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
                return !string.IsNullOrEmpty(secondEntry.Key) &&
                       entry.Value.SequenceEqual(secondEntry.Value, StringComparer.OrdinalIgnoreCase);
            });
    }
}
