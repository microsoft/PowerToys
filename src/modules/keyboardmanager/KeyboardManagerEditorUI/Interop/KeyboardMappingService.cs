// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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

            bool settingsLoaded = KeyboardManagerInterop.LoadMappingSettings(_configHandle);
            ConfigurationName = KeyboardManagerInterop.GetStringAndFree(KeyboardManagerInterop.GetMappingConfigurationName(_configHandle));
            if (!settingsLoaded &&
                (!KeyboardManagerInterop.MappingConfigurationNameWasResolved(_configHandle) ||
                 KeyboardManagerInterop.MappingSettingsFileExists(_configHandle)))
            {
                KeyboardManagerInterop.DestroyMappingConfiguration(_configHandle);
                _configHandle = IntPtr.Zero;
                throw new InvalidOperationException("Failed to load mapping configuration");
            }
        }

        internal string ConfigurationName { get; }

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

            // Also surface "Alone" (dual-key) remaps, tagged so the UI can show the condition.
            int aloneCount = KeyboardManagerInterop.GetSingleKeyAloneRemapCount(_configHandle);
            for (int i = 0; i < aloneCount; i++)
            {
                var mapping = default(SingleKeyMapping);
                if (KeyboardManagerInterop.GetSingleKeyAloneRemap(_configHandle, i, ref mapping))
                {
                    result.Add(new KeyMapping
                    {
                        OriginalKey = mapping.OriginalKey,
                        TargetKey = KeyboardManagerInterop.GetStringAndFree(mapping.TargetKey),
                        IsShortcut = mapping.IsShortcut,
                        IsAlone = true,
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
                        TargetKeys = CanonicalizeTargetKeys(
                            (ShortcutOperationType)mapping.OperationType,
                            KeyboardManagerInterop.GetStringAndFree(mapping.TargetKeys)),
                        TargetApp = KeyboardManagerInterop.GetStringAndFree(mapping.TargetApp),
                        OperationType = (ShortcutOperationType)mapping.OperationType,
                        ExactMatch = mapping.ExactMatch != 0,
                        TargetText = KeyboardManagerInterop.GetStringAndFree(mapping.TargetText),
                        ProgramPath = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramPath),
                        ProgramArgs = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramArgs),
                        StartInDirectory = KeyboardManagerInterop.GetStringAndFree(mapping.StartInDirectory),
                        Elevation = (ShortcutKeyMapping.ElevationLevel)mapping.Elevation,
                        IfRunningAction = (ShortcutKeyMapping.ProgramAlreadyRunningAction)mapping.IfRunningAction,
                        Visibility = (ShortcutKeyMapping.StartWindowType)mapping.Visibility,
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
                        TargetKeys = CanonicalizeTargetKeys(
                            (ShortcutOperationType)mapping.OperationType,
                            KeyboardManagerInterop.GetStringAndFree(mapping.TargetKeys)),
                        TargetApp = KeyboardManagerInterop.GetStringAndFree(mapping.TargetApp),
                        OperationType = (ShortcutOperationType)mapping.OperationType,
                        ExactMatch = mapping.ExactMatch != 0,
                        TargetText = KeyboardManagerInterop.GetStringAndFree(mapping.TargetText),
                        ProgramPath = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramPath),
                        ProgramArgs = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramArgs),
                        StartInDirectory = KeyboardManagerInterop.GetStringAndFree(mapping.StartInDirectory),
                        Elevation = (ShortcutKeyMapping.ElevationLevel)mapping.Elevation,
                        IfRunningAction = (ShortcutKeyMapping.ProgramAlreadyRunningAction)mapping.IfRunningAction,
                        Visibility = (ShortcutKeyMapping.StartWindowType)mapping.Visibility,
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

        public bool AddSingleKeyAloneMapping(int originalKey, int targetKey)
        {
            return KeyboardManagerInterop.AddSingleKeyAloneRemap(_configHandle, originalKey, targetKey);
        }

        public bool AddSingleKeyAloneMapping(int originalKey, string targetKeys)
        {
            if (string.IsNullOrEmpty(targetKeys))
            {
                return false;
            }

            if (!targetKeys.Contains(';') && int.TryParse(targetKeys, out int targetKey))
            {
                return KeyboardManagerInterop.AddSingleKeyAloneRemap(_configHandle, originalKey, targetKey);
            }
            else
            {
                return KeyboardManagerInterop.AddSingleKeyAloneToShortcutRemap(_configHandle, originalKey, targetKeys);
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
            if (string.IsNullOrEmpty(shortcutKeyMapping.OriginalKeys) || string.IsNullOrEmpty(GetNativeTargetKeys(shortcutKeyMapping)))
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
                    GetNativeTargetKeys(shortcutKeyMapping),
                    shortcutKeyMapping.TargetApp,
                    (int)shortcutKeyMapping.OperationType,
                    shortcutKeyMapping.ProgramPath,
                    string.IsNullOrEmpty(shortcutKeyMapping.ProgramArgs) ? null : shortcutKeyMapping.ProgramArgs,
                    string.IsNullOrEmpty(shortcutKeyMapping.StartInDirectory) ? null : shortcutKeyMapping.StartInDirectory,
                    (int)shortcutKeyMapping.Elevation,
                    (int)shortcutKeyMapping.IfRunningAction,
                    (int)shortcutKeyMapping.Visibility,
                    shortcutKeyMapping.ExactMatch ? 1 : 0);
            }
            else if (shortcutKeyMapping.OperationType == ShortcutOperationType.OpenUri)
            {
                return KeyboardManagerInterop.AddShortcutRemap(
                    _configHandle,
                    shortcutKeyMapping.OriginalKeys,
                    GetNativeTargetKeys(shortcutKeyMapping),
                    shortcutKeyMapping.TargetApp,
                    (int)shortcutKeyMapping.OperationType,
                    shortcutKeyMapping.UriToOpen,
                    exactMatch: shortcutKeyMapping.ExactMatch ? 1 : 0);
            }

            return KeyboardManagerInterop.AddShortcutRemap(
                _configHandle,
                shortcutKeyMapping.OriginalKeys,
                GetNativeTargetKeys(shortcutKeyMapping),
                shortcutKeyMapping.TargetApp,
                (int)shortcutKeyMapping.OperationType,
                exactMatch: shortcutKeyMapping.ExactMatch ? 1 : 0);
        }

        public bool SaveSettings()
        {
            return KeyboardManagerInterop.SaveMappingSettings(_configHandle);
        }

        internal bool SaveSettingsAndVerify()
        {
            if (!SaveSettings())
            {
                return false;
            }

            try
            {
                using var persistedService = new KeyboardMappingService();
                return HasSameMappings(persistedService);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to verify saved mapping settings: " + ex.Message);
                return false;
            }
        }

        internal bool HasSameMappings(KeyboardMappingService other) =>
            ConfigurationName.Equals(other.ConfigurationName, StringComparison.OrdinalIgnoreCase) &&
            MappingCollectionsEqual(
                GetSingleKeyMappings(),
                other.GetSingleKeyMappings(),
                GetKeyToTextMappings(),
                other.GetKeyToTextMappings(),
                GetShortcutMappings(),
                other.GetShortcutMappings());

        internal static bool MappingCollectionsEqual(
            IEnumerable<KeyMapping> firstSingleKeyMappings,
            IEnumerable<KeyMapping> secondSingleKeyMappings,
            IEnumerable<KeyToTextMapping> firstTextMappings,
            IEnumerable<KeyToTextMapping> secondTextMappings,
            IEnumerable<ShortcutKeyMapping> firstShortcutMappings,
            IEnumerable<ShortcutKeyMapping> secondShortcutMappings)
        {
            var firstSingleKeys = firstSingleKeyMappings.OrderBy(mapping => mapping.OriginalKey).ToList();
            var secondSingleKeys = secondSingleKeyMappings.OrderBy(mapping => mapping.OriginalKey).ToList();
            var firstTexts = firstTextMappings.OrderBy(mapping => mapping.OriginalKey).ToList();
            var secondTexts = secondTextMappings.OrderBy(mapping => mapping.OriginalKey).ToList();
            var firstShortcuts = OrderShortcutMappings(firstShortcutMappings).ToList();
            var secondShortcuts = OrderShortcutMappings(secondShortcutMappings).ToList();

            return firstSingleKeys.Count == secondSingleKeys.Count &&
                   firstSingleKeys.Zip(secondSingleKeys).All(pair =>
                       pair.First.OriginalKey == pair.Second.OriginalKey &&
                       string.Equals(pair.First.TargetKey, pair.Second.TargetKey, StringComparison.Ordinal) &&
                       pair.First.IsShortcut == pair.Second.IsShortcut) &&
                   firstTexts.Count == secondTexts.Count &&
                   firstTexts.Zip(secondTexts).All(pair =>
                       pair.First.OriginalKey == pair.Second.OriginalKey &&
                       string.Equals(pair.First.TargetText, pair.Second.TargetText, StringComparison.Ordinal)) &&
                   firstShortcuts.Count == secondShortcuts.Count &&
                   firstShortcuts.Zip(secondShortcuts).All(pair => ShortcutMappingsEqual(pair.First, pair.Second));
        }

        internal static string CanonicalizeTargetKeys(ShortcutOperationType operationType, string targetKeys) =>
            operationType is ShortcutOperationType.RunProgram or ShortcutOperationType.OpenUri or ShortcutOperationType.RemapText
                ? string.Empty
                : targetKeys;

        internal static string GetNativeTargetKeys(ShortcutKeyMapping mapping) => mapping.OperationType switch
        {
            ShortcutOperationType.RunProgram or ShortcutOperationType.OpenUri when string.IsNullOrEmpty(mapping.TargetKeys) => mapping.OriginalKeys,
            ShortcutOperationType.RemapText => mapping.TargetText,
            _ => mapping.TargetKeys,
        };

        public bool DeleteSingleKeyMapping(int originalKey)
        {
            return KeyboardManagerInterop.DeleteSingleKeyRemap(_configHandle, originalKey);
        }

        public bool DeleteSingleKeyAloneMapping(int originalKey)
        {
            return KeyboardManagerInterop.DeleteSingleKeyAloneRemap(_configHandle, originalKey);
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

            return KeyboardManagerInterop.DeleteShortcutRemap(_configHandle, originalKeys, (targetApp ?? string.Empty).ToLowerInvariant());
        }

        private static IOrderedEnumerable<ShortcutKeyMapping> OrderShortcutMappings(IEnumerable<ShortcutKeyMapping> mappings) =>
            mappings
                .OrderBy(mapping => mapping.OriginalKeys, StringComparer.Ordinal)
                .ThenBy(mapping => mapping.TargetApp, StringComparer.OrdinalIgnoreCase)
                .ThenBy(mapping => mapping.OperationType);

        private static bool ShortcutMappingsEqual(ShortcutKeyMapping first, ShortcutKeyMapping second) =>
            string.Equals(first.OriginalKeys, second.OriginalKeys, StringComparison.Ordinal) &&
            string.Equals(first.TargetKeys, second.TargetKeys, StringComparison.Ordinal) &&
            string.Equals(first.TargetApp, second.TargetApp, StringComparison.OrdinalIgnoreCase) &&
            first.OperationType == second.OperationType &&
            first.ExactMatch == second.ExactMatch &&
            string.Equals(first.TargetText, second.TargetText, StringComparison.Ordinal) &&
            string.Equals(first.ProgramPath, second.ProgramPath, StringComparison.Ordinal) &&
            string.Equals(first.ProgramArgs, second.ProgramArgs, StringComparison.Ordinal) &&
            string.Equals(first.StartInDirectory, second.StartInDirectory, StringComparison.Ordinal) &&
            first.Elevation == second.Elevation &&
            first.IfRunningAction == second.IfRunningAction &&
            first.Visibility == second.Visibility &&
            string.Equals(first.UriToOpen, second.UriToOpen, StringComparison.Ordinal);

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
