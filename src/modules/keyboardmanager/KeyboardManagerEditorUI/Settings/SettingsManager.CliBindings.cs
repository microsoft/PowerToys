// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using KeyboardManagerEditorUI.Interop;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Settings
{
    internal static partial class SettingsManager
    {
        /// <summary>
        /// Saves a structured CLI binding and its native RunProgram mapping as one editor operation.
        /// Existing bindings retain their ID, profiles, and activation state. Single-key mappings
        /// cannot be converted because a CLI action requires a shortcut trigger.
        /// </summary>
        public static bool TrySaveCliBinding(
            KeyboardMappingService service,
            string? existingId,
            ShortcutKeyMapping newMapping,
            CliBindingSettings binding,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (!ValidateCliMapping(newMapping, binding, out error))
                {
                    return false;
                }

                ShortcutSettings? previousSettings = null;
                if (!string.IsNullOrEmpty(existingId))
                {
                    if (!EditorSettings.ShortcutSettingsDictionary.TryGetValue(existingId, out previousSettings))
                    {
                        error = "The mapping no longer exists. Reload Keyboard Manager and try again.";
                        return false;
                    }

                    if (previousSettings.Shortcut.OriginalKeys.Split(';').Length < 2)
                    {
                        error = "A single-key mapping cannot be converted to a CLI command. Create a new shortcut instead.";
                        return false;
                    }
                }

                var nativeMappings = service.GetShortcutMappings();
                if (!TryGetPreviousNativeMapping(previousSettings, nativeMappings, out ShortcutKeyMapping? previousMapping, out error))
                {
                    return false;
                }

                if (HasCliShortcutConflict(existingId, newMapping, previousMapping, nativeMappings))
                {
                    error = "This shortcut is already assigned for the selected app.";
                    return false;
                }

                // Stage the complete editor document before mutating either persisted file.
                // Failed saves leave the original in-memory editor snapshot untouched.
                EditorSettings updatedSettings = CloneSettingsValue(EditorSettings);
                string id = previousSettings is null ? Guid.NewGuid().ToString() : existingId!;
                ShortcutSettings updatedBinding = previousSettings is null ? new ShortcutSettings { Id = id } :
                    updatedSettings.ShortcutSettingsDictionary[id];
                updatedBinding.Shortcut = CloneSettingsValue(newMapping);
                updatedBinding.CliBinding = CloneSettingsValue(binding);
                updatedSettings.ShortcutSettingsDictionary[id] = updatedBinding;

                foreach (var ids in updatedSettings.ShortcutsByOperationType.Values)
                {
                    ids.Remove(id);
                }

                if (!updatedSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RunProgram, out var programIds))
                {
                    programIds = new List<string>();
                    updatedSettings.ShortcutsByOperationType[ShortcutOperationType.RunProgram] = programIds;
                }

                programIds.Add(id);

                return TryCommitCliBindingSettings(
                    service,
                    updatedSettings,
                    previousMapping,
                    updatedBinding.IsActive ? updatedBinding.Shortcut : null,
                    out error);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to prepare CLI binding settings: {ex.Message}");
                error = "The CLI binding could not be saved.";
                return false;
            }
        }

        public static bool TrySetCliBindingActive(KeyboardMappingService service, string id, bool isActive, out string error)
        {
            error = string.Empty;
            try
            {
                if (!EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? settings) || settings.CliBinding is null)
                {
                    error = "The CLI binding no longer exists. Reload Keyboard Manager and try again.";
                    return false;
                }

                if (settings.IsActive == isActive)
                {
                    return true;
                }

                if (isActive && !ValidateCliMapping(settings.Shortcut, settings.CliBinding, out error))
                {
                    return false;
                }

                var nativeMappings = service.GetShortcutMappings();
                if (!TryGetPreviousNativeMapping(settings, nativeMappings, out ShortcutKeyMapping? previousMapping, out error))
                {
                    return false;
                }

                if (isActive && HasCliShortcutConflict(id, settings.Shortcut, previousMapping, nativeMappings))
                {
                    error = "This shortcut is already assigned for the selected app.";
                    return false;
                }

                EditorSettings updatedSettings = CloneSettingsValue(EditorSettings);
                updatedSettings.ShortcutSettingsDictionary[id].IsActive = isActive;
                return TryCommitCliBindingSettings(
                    service,
                    updatedSettings,
                    previousMapping,
                    isActive ? settings.Shortcut : null,
                    out error);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to change CLI binding activation: {ex.Message}");
                error = "The CLI binding activation could not be saved.";
                return false;
            }
        }

        public static bool TryDeleteCliBinding(KeyboardMappingService service, string id, out string error)
        {
            error = string.Empty;
            try
            {
                if (!EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? settings) || settings.CliBinding is null)
                {
                    error = "The CLI binding no longer exists. Reload Keyboard Manager and try again.";
                    return false;
                }

                if (!TryGetPreviousNativeMapping(settings, service.GetShortcutMappings(), out ShortcutKeyMapping? previousMapping, out error))
                {
                    return false;
                }

                EditorSettings updatedSettings = CloneSettingsValue(EditorSettings);
                updatedSettings.ShortcutSettingsDictionary.Remove(id);
                foreach (var ids in updatedSettings.ShortcutsByOperationType.Values.Concat(updatedSettings.ProfileDictionary.Values))
                {
                    ids.Remove(id);
                }

                return TryCommitCliBindingSettings(service, updatedSettings, previousMapping, null, out error);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to delete CLI binding: {ex.Message}");
                error = "The CLI binding could not be deleted.";
                return false;
            }
        }

        private static bool ValidateCliMapping(ShortcutKeyMapping mapping, CliBindingSettings binding, out string error)
        {
            error = string.Empty;
            if (mapping.OperationType != ShortcutOperationType.RunProgram ||
                string.IsNullOrWhiteSpace(mapping.ProgramPath) || string.IsNullOrWhiteSpace(binding.CommandId) ||
                binding.Parameters is null)
            {
                error = "Select a CLI command and a valid PowerScripts host before saving.";
                return false;
            }

            string[] keys = mapping.OriginalKeys.Split(';');
            bool hasActionKey = false;
            foreach (string key in keys)
            {
                if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int keyCode) || keyCode <= 0 || keyCode > ushort.MaxValue)
                {
                    error = "Record a valid shortcut before saving the CLI command.";
                    return false;
                }

                hasActionKey |= KeyboardManagerInterop.GetKeyType(keyCode) == (int)KeyType.Action;
            }

            if (keys.Length < 2 || !hasActionKey || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
                KeyboardManagerInterop.IsShortcutIllegal(mapping.OriginalKeys) ||
                !KeyboardManagerInterop.AreShortcutsEqual(mapping.OriginalKeys, mapping.TargetKeys))
            {
                error = "Record a valid shortcut before saving the CLI command.";
                return false;
            }

            return true;
        }

        private static bool ShortcutsOverlap(ShortcutKeyMapping left, ShortcutKeyMapping right)
        {
            return EncodedShortcutsEqual(left.OriginalKeys, right.OriginalKeys) &&
                (string.IsNullOrEmpty(left.TargetApp) || string.IsNullOrEmpty(right.TargetApp) ||
                 string.Equals(left.TargetApp, right.TargetApp, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasCliShortcutConflict(
            string? existingId,
            ShortcutKeyMapping newMapping,
            ShortcutKeyMapping? previousMapping,
            List<ShortcutKeyMapping> nativeMappings)
        {
            return EditorSettings.ShortcutSettingsDictionary.Any(entry =>
                    entry.Key != existingId && ShortcutsOverlap(entry.Value.Shortcut, newMapping)) ||
                nativeMappings.Any(mapping =>
                    (previousMapping is null || !ObservedShortcutMatches(previousMapping, mapping)) &&
                    ShortcutsOverlap(mapping, newMapping));
        }

        private static bool TryGetPreviousNativeMapping(
            ShortcutSettings? settings,
            List<ShortcutKeyMapping> nativeMappings,
            out ShortcutKeyMapping? previousMapping,
            out string error)
        {
            previousMapping = null;
            error = string.Empty;
            if (settings?.IsActive != true)
            {
                return true;
            }

            ShortcutKeyMapping? nativeMapping = nativeMappings.FirstOrDefault(m => ObservedShortcutMatches(settings.Shortcut, m));
            if (nativeMapping is null)
            {
                error = "The mapping changed outside this editor. Reload Keyboard Manager before editing it.";
                return false;
            }

            previousMapping = CloneSettingsValue(settings.Shortcut);

            // Native enumeration canonicalizes shortcut order and lowercases app names.
            // Use its identity for deletion, while retaining editor-only launch options.
            previousMapping.OriginalKeys = nativeMapping.OriginalKeys;
            previousMapping.TargetApp = nativeMapping.TargetApp;

            // RunProgram/OpenUri do not persist their dummy target keys in native settings.
            // The managed add API still requires a valid placeholder when restoring them.
            if (previousMapping.OperationType is ShortcutOperationType.RunProgram or ShortcutOperationType.OpenUri)
            {
                previousMapping.TargetKeys = previousMapping.OriginalKeys;
            }

            // The native wrapper reports shortcut-to-text mappings as RemapShortcut.
            if (!string.IsNullOrEmpty(previousMapping.TargetText))
            {
                previousMapping.OperationType = ShortcutOperationType.RemapText;
                previousMapping.TargetKeys = previousMapping.TargetText;
            }

            return true;
        }

        private static bool ObservedShortcutMatches(ShortcutKeyMapping expected, ShortcutKeyMapping actual)
        {
            if (!EncodedShortcutsEqual(expected.OriginalKeys, actual.OriginalKeys) ||
                !string.Equals(expected.TargetApp, actual.TargetApp, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Shortcut-to-text enumeration uses an empty TargetKeys field and reports operation 0.
            if (expected.OperationType == ShortcutOperationType.RemapText ||
                (expected.OperationType == ShortcutOperationType.RemapShortcut && !string.IsNullOrEmpty(expected.TargetText)))
            {
                return string.Equals(expected.TargetText, actual.TargetText, StringComparison.Ordinal) &&
                    string.IsNullOrEmpty(actual.TargetKeys);
            }

            if (expected.OperationType != actual.OperationType)
            {
                return false;
            }

            return expected.OperationType switch
            {
                ShortcutOperationType.RunProgram =>
                    string.Equals(expected.ProgramPath, actual.ProgramPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(expected.ProgramArgs, actual.ProgramArgs, StringComparison.Ordinal),
                ShortcutOperationType.OpenUri => string.Equals(expected.UriToOpen, actual.UriToOpen, StringComparison.Ordinal),
                _ => EncodedShortcutsEqual(expected.TargetKeys, actual.TargetKeys),
            };
        }

        private static bool EncodedShortcutsEqual(string left, string right)
        {
            // Editor metadata is persisted as JSON. Reject malformed key encodings before calling
            // the native Shortcut constructor, whose numeric parser expects valid virtual keys.
            return IsEncodedShortcut(left) && IsEncodedShortcut(right) &&
                KeyboardManagerInterop.AreShortcutsEqual(left, right);
        }

        private static bool IsEncodedShortcut(string keys)
        {
            return !string.IsNullOrEmpty(keys) && keys.Split(';').All(key =>
                int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int keyCode) &&
                keyCode >= 0 && keyCode <= ushort.MaxValue);
        }

        private static bool TryCommitCliBindingSettings(
            KeyboardMappingService service,
            EditorSettings updatedSettings,
            ShortcutKeyMapping? previousMapping,
            ShortcutKeyMapping? newMapping,
            out string error)
        {
            bool previousMappingRemoved = false;
            bool newMappingAdded = false;
            error = string.Empty;
            try
            {
                if (previousMapping is not null)
                {
                    if (!service.DeleteShortcutMapping(previousMapping.OriginalKeys, previousMapping.TargetApp))
                    {
                        error = "The previous mapping could not be replaced. Reload Keyboard Manager and try again.";
                        return false;
                    }

                    previousMappingRemoved = true;
                }

                if (newMapping is not null)
                {
                    if (!service.AddShortcutMapping(newMapping))
                    {
                        return FailCliBindingSave(
                            service,
                            previousMapping,
                            newMapping,
                            previousMappingRemoved,
                            newMappingAdded,
                            "The CLI shortcut could not be added.",
                            out error);
                    }

                    newMappingAdded = true;
                }

                if ((previousMappingRemoved || newMappingAdded) && !service.SaveSettings())
                {
                    return FailCliBindingSave(
                        service,
                        previousMapping,
                        newMapping,
                        previousMappingRemoved,
                        newMappingAdded,
                        "The native Keyboard Manager settings could not be saved.",
                        out error);
                }

                if (!WriteSettings(updatedSettings))
                {
                    return FailCliBindingSave(
                        service,
                        previousMapping,
                        newMapping,
                        previousMappingRemoved,
                        newMappingAdded,
                        "The CLI binding settings could not be saved.",
                        out error);
                }

                EditorSettings = updatedSettings;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to commit CLI binding settings: {ex.Message}");
                return FailCliBindingSave(
                    service,
                    previousMapping,
                    newMapping,
                    previousMappingRemoved,
                    newMappingAdded,
                    "The CLI binding change could not be saved.",
                    out error);
            }
        }

        private static bool FailCliBindingSave(
            KeyboardMappingService service,
            ShortcutKeyMapping? previousMapping,
            ShortcutKeyMapping? newMapping,
            bool previousMappingRemoved,
            bool newMappingAdded,
            string message,
            out string error)
        {
            error = message;
            if (!previousMappingRemoved && !newMappingAdded)
            {
                return false;
            }

            bool restored = true;
            try
            {
                if (newMappingAdded && newMapping is not null)
                {
                    // The wrapper lowercases app names when adding a native mapping.
                    restored &= service.DeleteShortcutMapping(newMapping.OriginalKeys, newMapping.TargetApp.ToLowerInvariant());
                }

                if (previousMappingRemoved && previousMapping is not null)
                {
                    restored &= service.AddShortcutMapping(previousMapping);
                }

                restored &= service.SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to restore CLI binding settings: {ex.Message}");
                restored = false;
            }

            if (!restored)
            {
                error += " The previous native mapping could not be fully restored. Reload Keyboard Manager before making further changes.";
                Logger.LogError(error);
            }

            return false;
        }

        private static T CloneSettingsValue<T>(T value)
            where T : class
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, _jsonOptions), _jsonOptions) ??
                throw new InvalidOperationException("The editor settings snapshot could not be created.");
        }
    }
}
