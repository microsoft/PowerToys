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

        private static readonly string _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager");

        private static readonly string _settingsFilePath = Path.Combine(_settingsDirectory, "editorSettings.json");

        private static readonly string _transactionLockFilePath = Path.Combine(_settingsDirectory, "editorTransaction.lock");

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static readonly KeyboardMappingService? _mappingService;

        /// <summary>
        /// Gets a value indicating whether the native C++ wrapper DLL is available.
        /// When false the editor runs in read-only / XAML-preview mode using JSON settings only.
        /// </summary>
        internal static bool IsNativeServiceAvailable => _mappingService is not null;

        public static EditorSettings EditorSettings { get; set; }

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
                if (!File.Exists(_settingsFilePath))
                {
                    if (_mappingService is not null)
                    {
                        EditorSettings createdSettings = CreateSettingsFromKeyboardManagerService(_mappingService);
                        WriteSettings(createdSettings);
                        return createdSettings;
                    }

                    return new EditorSettings();
                }

                string json = File.ReadAllText(_settingsFilePath);
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
                Directory.CreateDirectory(_settingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);
                temporaryPath = Path.Combine(_settingsDirectory, $"editorSettings.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, _settingsFilePath, overwrite: true);
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
            Directory.CreateDirectory(_settingsDirectory);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
            do
            {
                try
                {
                    return new FileStream(
                        _transactionLockFilePath,
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

        internal static bool TryReloadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return false;
                }

                string json = File.ReadAllText(_settingsFilePath);
                EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
                NormalizeSettings(settings);
                EditorSettings = settings;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsFileLockContention(IOException exception)
        {
            int errorCode = exception.HResult & 0xFFFF;
            return errorCode is 32 or 33;
        }

        private static EditorSettings CreateSettingsFromKeyboardManagerService(KeyboardMappingService service)
        {
            EditorSettings settings = new EditorSettings
            {
                ActiveProfile = service.ConfigurationName,
            };

            // Process all shortcut mappings (RunProgram, OpenUri, RemapShortcut, RemapText)
            foreach (ShortcutKeyMapping mapping in service.GetShortcutMappings())
            {
                AddShortcutMapping(settings, mapping, settings.ActiveProfile);
            }

            // Process single key to key mappings
            foreach (var mapping in service.GetSingleKeyMappings())
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapShortcut,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetKey,

                    // GetSingleKeyMappings surfaces both the regular and the "alone" (dual-key) tables,
                    // tagging the latter with IsAlone; preserve that as the per-entry Condition so an
                    // alone remap round-trips instead of loading back as an unconditional (Always) one.
                    Condition = mapping.IsAlone ? SingleKeyRemapCondition.Alone : SingleKeyRemapCondition.Always,
                };
                AddShortcutMapping(settings, shortcutMapping, settings.ActiveProfile);
            }

            // Process single key to text mappings
            foreach (var mapping in service.GetKeyToTextMappings())
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetText = mapping.TargetText,
                };
                AddShortcutMapping(settings, shortcutMapping, settings.ActiveProfile);
            }

            return settings;
        }

        public static void CorrelateServiceAndEditorMappings()
        {
            if (_mappingService is null)
            {
                return;
            }

            using FileStream? transactionLock = TryAcquireMappingTransactionLock();
            if (transactionLock == null)
            {
                ManagedCommon.Logger.LogError("Could not acquire the Keyboard Manager editor transaction lock during reconciliation.");
                return;
            }

            try
            {
                using var service = new KeyboardMappingService();
                if (!TryReloadSettings())
                {
                    EditorSettings createdSettings = CreateSettingsFromKeyboardManagerService(service);
                    if (WriteSettings(createdSettings))
                    {
                        EditorSettings = createdSettings;
                    }

                    return;
                }

                var activeMappings = service.GetShortcutMappings();
                activeMappings.AddRange(service.GetSingleKeyMappings().Select(mapping => new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapShortcut,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetKey,
                    Condition = mapping.IsAlone ? SingleKeyRemapCondition.Alone : SingleKeyRemapCondition.Always,
                }));
                activeMappings.AddRange(service.GetKeyToTextMappings().Select(mapping => new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetText = mapping.TargetText,
                }));

                EditorSettings updatedSettings = CloneSettings(EditorSettings);
                if (ReconcileMappings(updatedSettings, activeMappings, service.ConfigurationName) &&
                    WriteSettings(updatedSettings))
                {
                    EditorSettings = updatedSettings;
                }
            }
            catch (Exception exception)
            {
                ManagedCommon.Logger.LogError("Failed to reconcile Keyboard Manager editor settings: " + exception.Message);
            }
        }

        internal static bool ReconcileMappings(
            EditorSettings settings,
            IEnumerable<ShortcutKeyMapping> activeMappings,
            string? profileName = null)
        {
            bool profileAware = !string.IsNullOrWhiteSpace(profileName);
            bool shortcutSettingsChanged = NormalizeSettings(settings);
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

        public static bool TryCommitShortcutKeyMapping(ShortcutKeyMapping shortcutKeyMapping, string? replacingId)
        {
            try
            {
                EditorSettings updatedSettings = CloneSettings();

                if (!TryApplyShortcutKeyMapping(updatedSettings, shortcutKeyMapping, replacingId, _mappingService?.ConfigurationName))
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

        public static bool TryRemoveShortcutKeyMapping(string guid)
        {
            try
            {
                EditorSettings updatedSettings = CloneSettings();
                if (!TryApplyShortcutKeyMappingRemoval(updatedSettings, guid, _mappingService?.ConfigurationName) ||
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

        public static bool TrySetShortcutKeyMappingActiveState(string guid, bool isActive)
        {
            try
            {
                EditorSettings updatedSettings = CloneSettings();
                if (!TryApplyShortcutKeyMappingActiveState(
                        updatedSettings,
                        guid,
                        isActive,
                        _mappingService?.ConfigurationName) ||
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

        internal static bool IsMappingInActiveProfile(ShortcutSettings shortcutSettings)
        {
            string? profileName = _mappingService?.ConfigurationName;
            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = EditorSettings.ActiveProfile;
            }

            return string.IsNullOrWhiteSpace(profileName) ||
                   GetProfiles(shortcutSettings).Count == 0 ||
                   BelongsToProfile(shortcutSettings, profileName);
        }

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
