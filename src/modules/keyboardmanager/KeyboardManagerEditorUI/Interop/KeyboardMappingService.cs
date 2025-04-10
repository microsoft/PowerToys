// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

        public bool AddShortcutMapping(string originalKeys, string targetKeys, string targetApp = "")
        {
            if (string.IsNullOrEmpty(originalKeys) || string.IsNullOrEmpty(targetKeys))
            {
                return false;
            }

            return KeyboardManagerInterop.AddShortcutRemap(_configHandle, originalKeys, targetKeys, targetApp);
        }

        public bool SaveSettings()
        {
            return KeyboardManagerInterop.SaveMappingSettings(_configHandle);
        }

        public bool DeleteSingleKeyMapping(int originalKey)
        {
            return KeyboardManagerInterop.DeleteSingleKeyRemap(_configHandle, originalKey);
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
