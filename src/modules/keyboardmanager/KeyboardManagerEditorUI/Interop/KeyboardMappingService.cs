// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Helpers;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Interop
{
    public class KeyboardMappingService : IDisposable
    {
        private IntPtr _configHandle;
        private bool _disposed;

        public KeyboardMappingService()
        {
            _configHandle = KeyboardManagerInterop.CreateMappingConfiguration();
            if (_configHandle == IntPtr.Zero)
            {
                Logger.LogError("Failed to create mapping configuration");
                throw new InvalidOperationException("Failed to create mapping configuration");
            }

            MappingConfigurationLoadResult loadResult = (MappingConfigurationLoadResult)KeyboardManagerInterop.LoadMappingSettingsWithResult(_configHandle);
            if (loadResult != MappingConfigurationLoadResult.Success)
            {
                KeyboardManagerInterop.DestroyMappingConfiguration(_configHandle);
                _configHandle = IntPtr.Zero;
                throw new InvalidOperationException($"Failed to load mapping configuration: {loadResult}");
            }
        }

        public List<KeyMapping> GetSingleKeyMappings()
        {
            var result = new List<KeyMapping>();
            int count = KeyboardManagerInterop.GetSingleKeyRemapCount(_configHandle);

            for (int i = 0; i < count; i++)
            {
                var mapping = default(SingleKeyMapping);
                if (KeyboardManagerInterop.GetSingleKeyRemap(_configHandle, i, ref mapping))
                {
                    result.Add(new KeyMapping
                    {
                        OriginalKey = mapping.OriginalKey,
                        TargetKey = KeyboardManagerInterop.GetStringAndFree(mapping.TargetKey),
                        IsShortcut = mapping.IsShortcut,
                    });
                }
            }

            return result;
        }

        public List<ShortcutKeyMapping> GetShortcutMappings()
        {
            var result = new List<ShortcutKeyMapping>();
            int count = KeyboardManagerInterop.GetShortcutRemapCount(_configHandle);

            for (int i = 0; i < count; i++)
            {
                var mapping = default(ShortcutMapping);
                if (KeyboardManagerInterop.GetShortcutRemap(_configHandle, i, ref mapping))
                {
                    result.Add(new ShortcutKeyMapping
                    {
                        OriginalKeys = KeyboardManagerInterop.GetStringAndFree(mapping.OriginalKeys),
                        TargetKeys = KeyboardManagerInterop.GetStringAndFree(mapping.TargetKeys),
                        TargetApp = KeyboardManagerInterop.GetStringAndFree(mapping.TargetApp),
                        OperationType = (ShortcutOperationType)mapping.OperationType,
                        TargetText = KeyboardManagerInterop.GetStringAndFree(mapping.TargetText),
                        ProgramPath = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramPath),
                        ProgramArgs = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramArgs),
                        UriToOpen = KeyboardManagerInterop.GetStringAndFree(mapping.UriToOpen),
                    });
                }
            }

            return result;
        }

        public List<ShortcutKeyMapping> GetShortcutMappingsByType(ShortcutOperationType operationType)
        {
            var result = new List<ShortcutKeyMapping>();
            int count = KeyboardManagerInterop.GetShortcutRemapCountByType(_configHandle, (int)operationType);

            for (int i = 0; i < count; i++)
            {
                var mapping = default(ShortcutMapping);
                if (KeyboardManagerInterop.GetShortcutRemapByType(_configHandle, (int)operationType, i, ref mapping))
                {
                    result.Add(new ShortcutKeyMapping
                    {
                        OriginalKeys = KeyboardManagerInterop.GetStringAndFree(mapping.OriginalKeys),
                        TargetKeys = KeyboardManagerInterop.GetStringAndFree(mapping.TargetKeys),
                        TargetApp = KeyboardManagerInterop.GetStringAndFree(mapping.TargetApp),
                        OperationType = (ShortcutOperationType)mapping.OperationType,
                        TargetText = KeyboardManagerInterop.GetStringAndFree(mapping.TargetText),
                        ProgramPath = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramPath),
                        ProgramArgs = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramArgs),
                        UriToOpen = KeyboardManagerInterop.GetStringAndFree(mapping.UriToOpen),
                    });
                }
            }

            return result;
        }

        public List<KeyToTextMapping> GetKeyToTextMappings()
        {
            var result = new List<KeyToTextMapping>();
            int count = KeyboardManagerInterop.GetSingleKeyToTextRemapCount(_configHandle);

            for (int i = 0; i < count; i++)
            {
                var mapping = default(KeyboardTextMapping);
                if (KeyboardManagerInterop.GetSingleKeyToTextRemap(_configHandle, i, ref mapping))
                {
                    result.Add(new KeyToTextMapping
                    {
                        OriginalKey = mapping.OriginalKey,
                        TargetText = KeyboardManagerInterop.GetStringAndFree(mapping.TargetText),
                    });
                }
            }

            return result;
        }

        public List<TextExpansionMapping> GetTextExpansionMappings()
        {
            var result = new List<TextExpansionMapping>();
            int count = KeyboardManagerInterop.GetTextExpansionCount(_configHandle);

            for (int index = 0; index < count; index++)
            {
                var nativeMapping = default(NativeTextExpansionMapping);
                if (!KeyboardManagerInterop.GetTextExpansion(_configHandle, index, ref nativeMapping))
                {
                    FreeNativeString(ref nativeMapping.Id);
                    FreeNativeString(ref nativeMapping.SourceText);
                    FreeNativeString(ref nativeMapping.ActivationKeys);
                    FreeNativeString(ref nativeMapping.ReplacementText);
                    continue;
                }

                try
                {
                    string id = TakeNativeString(ref nativeMapping.Id);
                    string sourceText = TakeNativeString(ref nativeMapping.SourceText);
                    string activationKeyString = TakeNativeString(ref nativeMapping.ActivationKeys);
                    string replacementText = TakeNativeString(ref nativeMapping.ReplacementText);
                    List<int> keyCodes = ParseKeyCodes(activationKeyString);

                    result.Add(new TextExpansionMapping
                    {
                        Id = id,
                        SourceText = sourceText,
                        ActivationKeys = keyCodes,
                        ActivationKeyNames = keyCodes.Select(GetKeyDisplayName).ToList(),
                        ReplacementText = replacementText,
                        IsEnabled = nativeMapping.Enabled,
                    });
                }
                finally
                {
                    FreeNativeString(ref nativeMapping.Id);
                    FreeNativeString(ref nativeMapping.SourceText);
                    FreeNativeString(ref nativeMapping.ActivationKeys);
                    FreeNativeString(ref nativeMapping.ReplacementText);
                }
            }

            return result;
        }

        public string GetKeyDisplayName(int keyCode)
        {
            var keyName = new StringBuilder(64);
            KeyboardManagerInterop.GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
        }

        public int GetKeyCodeFromName(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return 0;
            }

            int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(keyName);
            Logger.LogInfo($"Key code for key name {keyName}: {keyCode}");
            return keyCode;
        }

        public List<KeyNameEntry> GetKeyboardKeysList(bool isShortcut)
        {
            const int maxKeys = 512;
            var buffer = new KeyNamePair[maxKeys];
            int count = KeyboardManagerInterop.GetKeyboardKeysList(isShortcut, buffer, maxKeys);

            var result = new List<KeyNameEntry>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(new KeyNameEntry(buffer[i].KeyCode, buffer[i].KeyName));
            }

            return result;
        }

        public bool AddSingleKeyMapping(int originalKey, int targetKey)
        {
            return KeyboardManagerInterop.AddSingleKeyRemap(_configHandle, originalKey, targetKey);
        }

        public bool AddSingleKeyMapping(int originalKey, string targetKeys)
        {
            if (string.IsNullOrEmpty(targetKeys))
            {
                return false;
            }

            if (!targetKeys.Contains(';') && int.TryParse(targetKeys, out int targetKey))
            {
                return KeyboardManagerInterop.AddSingleKeyRemap(_configHandle, originalKey, targetKey);
            }
            else
            {
                return KeyboardManagerInterop.AddSingleKeyToShortcutRemap(_configHandle, originalKey, targetKeys);
            }
        }

        public bool AddSingleKeyToTextMapping(int originalKey, string targetText)
        {
            if (string.IsNullOrEmpty(targetText))
            {
                return false;
            }

            return KeyboardManagerInterop.AddSingleKeyToTextRemap(_configHandle, originalKey, targetText);
        }

        public bool AddShortcutMapping(string originalKeys, string targetKeys, string targetApp = "", ShortcutOperationType operationType = ShortcutOperationType.RemapShortcut)
        {
            if (string.IsNullOrEmpty(originalKeys) || string.IsNullOrEmpty(targetKeys))
            {
                return false;
            }

            return KeyboardManagerInterop.AddShortcutRemap(_configHandle, originalKeys, targetKeys, targetApp, (int)operationType);
        }

        public bool AddShortcutMapping(ShortcutKeyMapping shortcutKeyMapping)
        {
            if (string.IsNullOrEmpty(shortcutKeyMapping.OriginalKeys) || string.IsNullOrEmpty(shortcutKeyMapping.TargetKeys))
            {
                return false;
            }

            if (shortcutKeyMapping.OperationType == ShortcutOperationType.RunProgram && string.IsNullOrEmpty(shortcutKeyMapping.ProgramPath))
            {
                return false;
            }

            if (shortcutKeyMapping.OperationType == ShortcutOperationType.OpenUri && string.IsNullOrEmpty(shortcutKeyMapping.UriToOpen))
            {
                return false;
            }

            if (shortcutKeyMapping.OperationType == ShortcutOperationType.RunProgram)
            {
                return KeyboardManagerInterop.AddShortcutRemap(
                    _configHandle,
                    shortcutKeyMapping.OriginalKeys,
                    shortcutKeyMapping.TargetKeys,
                    shortcutKeyMapping.TargetApp,
                    (int)shortcutKeyMapping.OperationType,
                    shortcutKeyMapping.ProgramPath,
                    string.IsNullOrEmpty(shortcutKeyMapping.ProgramArgs) ? null : shortcutKeyMapping.ProgramArgs,
                    string.IsNullOrEmpty(shortcutKeyMapping.StartInDirectory) ? null : shortcutKeyMapping.StartInDirectory,
                    (int)shortcutKeyMapping.Elevation,
                    (int)shortcutKeyMapping.IfRunningAction,
                    (int)shortcutKeyMapping.Visibility);
            }
            else if (shortcutKeyMapping.OperationType == ShortcutOperationType.OpenUri)
            {
                return KeyboardManagerInterop.AddShortcutRemap(
                    _configHandle,
                    shortcutKeyMapping.OriginalKeys,
                    shortcutKeyMapping.TargetKeys,
                    shortcutKeyMapping.TargetApp,
                    (int)shortcutKeyMapping.OperationType,
                    shortcutKeyMapping.UriToOpen);
            }

            return KeyboardManagerInterop.AddShortcutRemap(
                _configHandle,
                shortcutKeyMapping.OriginalKeys,
                shortcutKeyMapping.TargetKeys,
                shortcutKeyMapping.TargetApp,
                (int)shortcutKeyMapping.OperationType);
        }

        public bool AddTextExpansionMapping(TextExpansionMapping mapping)
        {
            return IsValidTextExpansion(mapping) && KeyboardManagerInterop.AddTextExpansion(
                _configHandle,
                mapping.Id,
                mapping.SourceText,
                FormatKeyCodes(mapping.ActivationKeys),
                mapping.ReplacementText,
                mapping.IsEnabled);
        }

        public bool UpdateTextExpansionMapping(TextExpansionMapping mapping)
        {
            return IsValidTextExpansion(mapping) && KeyboardManagerInterop.UpdateTextExpansion(
                _configHandle,
                mapping.Id,
                mapping.SourceText,
                FormatKeyCodes(mapping.ActivationKeys),
                mapping.ReplacementText,
                mapping.IsEnabled);
        }

        public bool DeleteTextExpansionMapping(string id)
        {
            return TextExpansionValidation.IsCanonicalGuid(id) && KeyboardManagerInterop.DeleteTextExpansion(_configHandle, id);
        }

        public bool SetTextExpansionEnabled(string id, bool enabled)
        {
            return TextExpansionValidation.IsCanonicalGuid(id) && KeyboardManagerInterop.SetTextExpansionEnabled(_configHandle, id, enabled);
        }

        public bool SaveSettings()
        {
            return KeyboardManagerInterop.SaveMappingSettings(_configHandle);
        }

        public bool ReloadSettings()
        {
            return (MappingConfigurationLoadResult)KeyboardManagerInterop.LoadMappingSettingsWithResult(_configHandle) == MappingConfigurationLoadResult.Success;
        }

        public bool DeleteSingleKeyMapping(int originalKey)
        {
            return KeyboardManagerInterop.DeleteSingleKeyRemap(_configHandle, originalKey);
        }

        public bool DeleteSingleKeyToTextMapping(int originalKey)
        {
            if (originalKey == 0)
            {
                return false;
            }

            return KeyboardManagerInterop.DeleteSingleKeyToTextRemap(_configHandle, originalKey);
        }

        public bool DeleteShortcutMapping(string originalKeys, string targetApp = "")
        {
            if (string.IsNullOrEmpty(originalKeys))
            {
                return false;
            }

            return KeyboardManagerInterop.DeleteShortcutRemap(_configHandle, originalKeys, targetApp ?? string.Empty);
        }

        private static bool IsValidTextExpansion(TextExpansionMapping? mapping)
        {
            return mapping != null &&
                   TextExpansionValidation.IsCanonicalGuid(mapping.Id) &&
                   TextExpansionValidation.IsValidSourceText(mapping.SourceText) &&
                   TextExpansionValidation.IsValidActivationKeys(mapping.ActivationKeys) &&
                   TextExpansionValidation.IsValidReplacementText(mapping.ReplacementText);
        }

        private static string FormatKeyCodes(IEnumerable<int> keyCodes)
        {
            return string.Join(";", keyCodes.Select(keyCode => keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        private static List<int> ParseKeyCodes(string keyCodes)
        {
            return keyCodes.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(keyCode => int.TryParse(keyCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed) ? parsed : 0)
                .Where(keyCode => keyCode > 0)
                .ToList();
        }

        private static string TakeNativeString(ref IntPtr pointer)
        {
            IntPtr value = pointer;
            pointer = IntPtr.Zero;
            return KeyboardManagerInterop.GetStringAndFree(value);
        }

        private static void FreeNativeString(ref IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(pointer);
                pointer = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_configHandle != IntPtr.Zero)
                {
                    KeyboardManagerInterop.DestroyMappingConfiguration(_configHandle);
                    _configHandle = IntPtr.Zero;
                }

                _disposed = true;
            }
        }

        ~KeyboardMappingService()
        {
            Dispose(false);
        }
    }
}
