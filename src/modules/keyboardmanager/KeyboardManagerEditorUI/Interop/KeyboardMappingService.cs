// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Interop
{
    public class KeyboardMappingService : IDisposable
    {
        /// <summary>
        /// Combined modifier key codes and the left/right pair each one stands for.
        /// </summary>
        /// <remarks>
        /// The engine matches single-key remaps against the raw vkCode reported by the low-level
        /// hook (<c>KeyboardEventHandlers::HandleSingleKeyRemapEvent</c>), which is always the
        /// side-specific code - it is never VK_CONTROL/VK_MENU/VK_SHIFT, and VK_WIN_BOTH is not a
        /// real virtual key at all. The key dropdown still offers the combined keys, so a remap
        /// stored under one of them would never fire. The classic editor works around this by
        /// expanding on save (<c>LoadingAndSavingRemappingHelper::ApplySingleKeyRemappings</c>).
        /// Reads retain physical keys so reconciliation can preserve the identities and profile
        /// memberships of existing left/right rows as well as combined rows.
        /// </remarks>
        internal static readonly (int Combined, int Left, int Right)[] CombinedModifierKeys =
        {
            (0x11, 0xA2, 0xA3),   // VK_CONTROL -> VK_LCONTROL, VK_RCONTROL
            (0x12, 0xA4, 0xA5),   // VK_MENU    -> VK_LMENU,    VK_RMENU
            (0x10, 0xA0, 0xA1),   // VK_SHIFT   -> VK_LSHIFT,   VK_RSHIFT
            (0x104, 0x5B, 0x5C),  // VK_WIN_BOTH-> VK_LWIN,     VK_RWIN
        };

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

            ConfigurationLoaded = LoadWithRetry();
        }

        /// <summary>
        /// Gets a value indicating whether the engine configuration was read successfully or a
        /// new configuration can safely be created.
        /// </summary>
        /// <remarks>
        /// When it was not, the in-memory configuration is empty but says nothing about the file on
        /// disk. Because this editor saves each mapping immediately, writing that empty
        /// configuration back would replace every remap the user has with whatever they just
        /// edited, so <see cref="SaveSettings"/> refuses while this is false.
        /// </remarks>
        public bool ConfigurationLoaded { get; }

        private bool LoadWithRetry()
        {
            if (KeyboardManagerInterop.LoadMappingSettingsForEditor(_configHandle) is MappingConfigurationLoadResult.Loaded or MappingConfigurationLoadResult.NewConfiguration)
            {
                return true;
            }

            // Same one-shot retry the classic editor does in its constructor: the engine may be
            // rewriting the file at the moment the editor starts.
            Logger.LogWarning("Failed to load the Keyboard Manager configuration, retrying once");
            System.Threading.Thread.Sleep(500);

            if (KeyboardManagerInterop.LoadMappingSettingsForEditor(_configHandle) is MappingConfigurationLoadResult.Loaded or MappingConfigurationLoadResult.NewConfiguration)
            {
                return true;
            }

            Logger.LogError("Could not load the Keyboard Manager configuration; the editor will not save changes");
            return false;
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
                        StartInDirectory = KeyboardManagerInterop.GetStringAndFree(mapping.StartInDirectory),
                        Elevation = (ShortcutKeyMapping.ElevationLevel)mapping.Elevation,
                        IfRunningAction = (ShortcutKeyMapping.ProgramAlreadyRunningAction)mapping.IfRunningAction,
                        Visibility = (ShortcutKeyMapping.StartWindowType)mapping.Visibility,
                        UriToOpen = KeyboardManagerInterop.GetStringAndFree(mapping.UriToOpen),
                        ExactMatch = mapping.ExactMatch != 0,
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
                        StartInDirectory = KeyboardManagerInterop.GetStringAndFree(mapping.StartInDirectory),
                        Elevation = (ShortcutKeyMapping.ElevationLevel)mapping.Elevation,
                        IfRunningAction = (ShortcutKeyMapping.ProgramAlreadyRunningAction)mapping.IfRunningAction,
                        Visibility = (ShortcutKeyMapping.StartWindowType)mapping.Visibility,
                        UriToOpen = KeyboardManagerInterop.GetStringAndFree(mapping.UriToOpen),
                        ExactMatch = mapping.ExactMatch != 0,
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
            return AddExpanded(originalKey, key => KeyboardManagerInterop.AddSingleKeyRemap(_configHandle, key, targetKey), isTextRemap: false);
        }

        public bool AddSingleKeyMapping(int originalKey, string targetKeys)
        {
            if (string.IsNullOrEmpty(targetKeys))
            {
                return false;
            }

            if (!targetKeys.Contains(';') && int.TryParse(targetKeys, out int targetKey))
            {
                return AddSingleKeyMapping(originalKey, targetKey);
            }

            return AddExpanded(originalKey, key => KeyboardManagerInterop.AddSingleKeyToShortcutRemap(_configHandle, key, targetKeys), isTextRemap: false);
        }

        public bool AddSingleKeyToTextMapping(int originalKey, string targetText)
        {
            if (string.IsNullOrEmpty(targetText))
            {
                return false;
            }

            return AddExpanded(originalKey, key => KeyboardManagerInterop.AddSingleKeyToTextRemap(_configHandle, key, targetText), isTextRemap: true);
        }

        public bool AddShortcutMapping(string originalKeys, string targetKeys, string targetApp = "", ShortcutOperationType operationType = ShortcutOperationType.RemapShortcut, bool exactMatch = false)
        {
            if (string.IsNullOrEmpty(originalKeys) || string.IsNullOrEmpty(targetKeys))
            {
                return false;
            }

            return KeyboardManagerInterop.AddShortcutRemap(_configHandle, originalKeys, targetKeys, targetApp, (int)operationType, exactMatch: exactMatch ? 1 : 0);
        }

        public bool AddShortcutMapping(ShortcutKeyMapping shortcutKeyMapping)
        {
            if (string.IsNullOrEmpty(shortcutKeyMapping.OriginalKeys))
            {
                return false;
            }

            // Program and URI mappings have no key target after a native configuration reload.
            // Their payload is carried in ProgramPath/UriToOpen instead.
            if ((shortcutKeyMapping.OperationType is ShortcutOperationType.RemapShortcut or ShortcutOperationType.RemapText) &&
                string.IsNullOrEmpty(shortcutKeyMapping.TargetKeys))
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
                    (int)shortcutKeyMapping.Visibility,
                    shortcutKeyMapping.ExactMatch ? 1 : 0);
            }
            else if (shortcutKeyMapping.OperationType == ShortcutOperationType.OpenUri)
            {
                return KeyboardManagerInterop.AddShortcutRemap(
                    _configHandle,
                    shortcutKeyMapping.OriginalKeys,
                    shortcutKeyMapping.TargetKeys,
                    shortcutKeyMapping.TargetApp,
                    (int)shortcutKeyMapping.OperationType,
                    shortcutKeyMapping.UriToOpen,
                    exactMatch: shortcutKeyMapping.ExactMatch ? 1 : 0);
            }

            return KeyboardManagerInterop.AddShortcutRemap(
                _configHandle,
                shortcutKeyMapping.OriginalKeys,
                shortcutKeyMapping.TargetKeys,
                shortcutKeyMapping.TargetApp,
                (int)shortcutKeyMapping.OperationType,
                exactMatch: shortcutKeyMapping.ExactMatch ? 1 : 0);
        }

        public bool SaveSettings()
        {
            if (!ConfigurationLoaded)
            {
                // Writing the empty in-memory configuration here would wipe every remap on disk.
                Logger.LogError("Refusing to save: the Keyboard Manager configuration was never loaded");
                return false;
            }

            return KeyboardManagerInterop.SaveMappingSettings(_configHandle);
        }

        public bool DeleteSingleKeyMapping(int originalKey)
        {
            return DeleteExpanded(originalKey, key => KeyboardManagerInterop.DeleteSingleKeyRemap(_configHandle, key));
        }

        public bool DeleteSingleKeyToTextMapping(int originalKey)
        {
            if (originalKey == 0)
            {
                return false;
            }

            return DeleteExpanded(originalKey, key => KeyboardManagerInterop.DeleteSingleKeyToTextRemap(_configHandle, key));
        }

        public bool DeleteShortcutMapping(string originalKeys, string targetApp = "")
        {
            if (string.IsNullOrEmpty(originalKeys))
            {
                return false;
            }

            return KeyboardManagerInterop.DeleteShortcutRemap(_configHandle, originalKeys, targetApp ?? string.Empty);
        }

        /// <summary>
        /// Returns the key codes the engine actually has to be told about for a given origin key:
        /// the left/right pair for a combined modifier, the key itself for anything else.
        /// </summary>
        internal static int[] ExpandCombinedModifier(int keyCode)
        {
            foreach (var (combined, left, right) in CombinedModifierKeys)
            {
                if (keyCode == combined)
                {
                    return new[] { left, right };
                }
            }

            return new[] { keyCode };
        }

        /// <summary>
        /// Applies <paramref name="add"/> to every expanded origin key, rolling back on failure so a
        /// combined modifier is never left half-mapped.
        /// </summary>
        private bool AddExpanded(int originalKey, Func<int, bool> add, bool isTextRemap)
        {
            int[] keys = ExpandCombinedModifier(originalKey);
            if (keys.Length == 1)
            {
                return add(keys[0]);
            }

            var added = new List<int>(keys.Length);
            foreach (int key in keys)
            {
                if (add(key))
                {
                    added.Add(key);
                    continue;
                }

                foreach (int done in added)
                {
                    if (isTextRemap)
                    {
                        KeyboardManagerInterop.DeleteSingleKeyToTextRemap(_configHandle, done);
                    }
                    else
                    {
                        KeyboardManagerInterop.DeleteSingleKeyRemap(_configHandle, done);
                    }
                }

                Logger.LogWarning($"Could not remap key {key} (expanded from {originalKey}); rolled the mapping back");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Deletes the original key when it exists, including combined virtual keys written by
        /// earlier editor versions. Otherwise deletes the expanded pair used by newer versions.
        /// A legacy combined row must not remove separately stored physical-key rows.
        /// </summary>
        private bool DeleteExpanded(int originalKey, Func<int, bool> delete)
        {
            if (delete(originalKey))
            {
                return true;
            }

            bool deletedAny = false;
            foreach (int key in ExpandCombinedModifier(originalKey))
            {
                if (key != originalKey)
                {
                    deletedAny |= delete(key);
                }
            }

            return deletedAny;
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
