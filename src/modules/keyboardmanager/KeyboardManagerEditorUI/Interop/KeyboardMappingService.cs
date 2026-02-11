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

            KeyboardManagerInterop.LoadMappingSettings(_configHandle);
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

            return KeyboardManagerInterop.GetKeyCodeFromName(keyName);
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

        // Mouse Button Remapping

        /// <summary>
        /// Gets all mouse button to key/shortcut/text/program/url remappings.
        /// </summary>
        public List<Helpers.MouseMapping> GetMouseButtonMappings()
        {
            var result = new List<Helpers.MouseMapping>();
            int count = KeyboardManagerInterop.GetMouseButtonRemapCount(_configHandle);

            for (int i = 0; i < count; i++)
            {
                var mapping = default(MouseButtonMapping);
                if (KeyboardManagerInterop.GetMouseButtonRemap(_configHandle, i, ref mapping))
                {
                    result.Add(new Helpers.MouseMapping
                    {
                        OriginalButtonCode = mapping.OriginalButton,
                        OriginalButton = GetMouseButtonName((MouseButtonCode)mapping.OriginalButton),
                        TargetType = ConvertTargetTypeToString(mapping.TargetType),
                        TargetKeyName = mapping.TargetType == 0 ? KeyboardManagerInterop.GetStringAndFree(mapping.TargetKeys) : string.Empty,
                        TargetShortcutKeys = mapping.TargetType == 1 ? KeyboardManagerInterop.GetStringAndFree(mapping.TargetKeys) : string.Empty,
                        TargetText = KeyboardManagerInterop.GetStringAndFree(mapping.TargetText),
                        ProgramPath = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramPath),
                        ProgramArgs = KeyboardManagerInterop.GetStringAndFree(mapping.ProgramArgs),
                        UriToOpen = KeyboardManagerInterop.GetStringAndFree(mapping.UriToOpen),
                        TargetApp = KeyboardManagerInterop.GetStringAndFree(mapping.TargetApp),
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Adds a mouse button remapping.
        /// </summary>
        /// <param name="originalButton">The original mouse button.</param>
        /// <param name="targetKeys">Target key/shortcut string.</param>
        /// <param name="targetApp">Target app (empty for global).</param>
        /// <param name="targetType">0=Key, 1=Shortcut, 2=Text, 3=RunProgram, 4=OpenUri.</param>
        /// <param name="targetText">Text for text mappings.</param>
        /// <param name="programPath">Program path for RunProgram.</param>
        /// <param name="programArgs">Program arguments for RunProgram.</param>
        /// <param name="uriToOpen">URI for OpenUri.</param>
        public bool AddMouseButtonMapping(
            MouseButtonCode originalButton,
            string targetKeys,
            string targetApp = "",
            int targetType = 0,
            string? targetText = null,
            string? programPath = null,
            string? programArgs = null,
            string? uriToOpen = null)
        {
            return KeyboardManagerInterop.AddMouseButtonRemap(
                _configHandle,
                (int)originalButton,
                targetKeys ?? string.Empty,
                targetApp ?? string.Empty,
                targetType,
                targetText ?? string.Empty,
                programPath ?? string.Empty,
                programArgs ?? string.Empty,
                uriToOpen ?? string.Empty);
        }

        /// <summary>
        /// Deletes a mouse button remapping.
        /// </summary>
        public bool DeleteMouseButtonMapping(MouseButtonCode originalButton, string targetApp = "")
        {
            return KeyboardManagerInterop.DeleteMouseButtonRemap(_configHandle, (int)originalButton, targetApp ?? string.Empty);
        }

        // Key to Mouse Remapping

        /// <summary>
        /// Gets all key to mouse button remappings.
        /// </summary>
        public List<Helpers.KeyToMouseMapping> GetKeyToMouseMappings()
        {
            var result = new List<Helpers.KeyToMouseMapping>();
            int count = KeyboardManagerInterop.GetKeyToMouseRemapCount(_configHandle);

            for (int i = 0; i < count; i++)
            {
                var mapping = default(KeyToMouseMappingInterop);
                if (KeyboardManagerInterop.GetKeyToMouseRemap(_configHandle, i, ref mapping))
                {
                    result.Add(new Helpers.KeyToMouseMapping
                    {
                        OriginalKeyCode = mapping.OriginalKey,
                        OriginalKeyName = GetKeyDisplayName(mapping.OriginalKey),
                        TargetMouseButton = GetMouseButtonName((MouseButtonCode)mapping.TargetMouseButton),
                        TargetApp = KeyboardManagerInterop.GetStringAndFree(mapping.TargetApp),
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Adds a key to mouse button remapping.
        /// </summary>
        public bool AddKeyToMouseMapping(int originalKey, MouseButtonCode targetMouseButton, string targetApp = "")
        {
            return KeyboardManagerInterop.AddKeyToMouseRemap(_configHandle, originalKey, (int)targetMouseButton, targetApp ?? string.Empty);
        }

        /// <summary>
        /// Deletes a key to mouse button remapping.
        /// </summary>
        public bool DeleteKeyToMouseMapping(int originalKey, string targetApp = "")
        {
            return KeyboardManagerInterop.DeleteKeyToMouseRemap(_configHandle, originalKey, targetApp ?? string.Empty);
        }

        // Mouse Utility Methods

        /// <summary>
        /// Gets the display name for a mouse button.
        /// </summary>
        public string GetMouseButtonName(MouseButtonCode buttonCode)
        {
            var buttonName = new System.Text.StringBuilder(64);
            KeyboardManagerInterop.GetMouseButtonName((int)buttonCode, buttonName, buttonName.Capacity);
            return buttonName.ToString();
        }

        /// <summary>
        /// Gets the mouse button code from a display name.
        /// </summary>
        public MouseButtonCode GetMouseButtonFromName(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName))
            {
                return MouseButtonCode.Left;
            }

            return (MouseButtonCode)KeyboardManagerInterop.GetMouseButtonFromName(buttonName);
        }

        private static string ConvertTargetTypeToString(int targetType)
        {
            return targetType switch
            {
                0 => "Key",
                1 => "Shortcut",
                2 => "Text",
                3 => "RunProgram",
                4 => "OpenUri",
                _ => "Key",
            };
        }

        public bool SaveSettings()
        {
            return KeyboardManagerInterop.SaveMappingSettings(_configHandle);
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
