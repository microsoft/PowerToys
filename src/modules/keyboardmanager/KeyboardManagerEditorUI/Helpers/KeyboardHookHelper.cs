// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Interop;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.System;

namespace KeyboardManagerEditorUI.Helpers
{
    public class KeyboardHookHelper : IDisposable
    {
        private static KeyboardHookHelper? _instance;

        public static KeyboardHookHelper Instance => _instance ??= new KeyboardHookHelper();

        private KeyboardMappingService _mappingService;

        private HotkeySettingsControlHook? _keyboardHook;

        // The active page using this keyboard hook
        private IKeyboardHookTarget? _activeTarget;

        private HashSet<VirtualKey> _currentlyPressedKeys = new();
        private List<VirtualKey> _keyPressOrder = new();

        private bool _disposed;

        // Singleton to make sure only one instance of the hook is active
        private KeyboardHookHelper()
        {
            _mappingService = new KeyboardMappingService();
        }

        public void ActivateHook(IKeyboardHookTarget target)
        {
            CleanupHook();

            _activeTarget = target;

            _currentlyPressedKeys.Clear();
            _keyPressOrder.Clear();

            _keyboardHook = new HotkeySettingsControlHook(
                KeyDown,
                KeyUp,
                () => true,
                (key, extraInfo) => true);
        }

        public void CleanupHook()
        {
            if (_keyboardHook != null)
            {
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }

            _currentlyPressedKeys.Clear();
            _keyPressOrder.Clear();
            _activeTarget = null;
        }

        private void KeyDown(int key)
        {
            if (_activeTarget == null)
            {
                return;
            }

            VirtualKey virtualKey = (VirtualKey)key;

            if (_currentlyPressedKeys.Contains(virtualKey))
            {
                return;
            }

            // if no keys are pressed, clear the lists when a new key is pressed
            if (_currentlyPressedKeys.Count == 0)
            {
                _activeTarget.ClearKeys();
                _keyPressOrder.Clear();
            }

            // Count current modifiers
            int modifierCount = _currentlyPressedKeys.Count(k => RemappingHelper.IsModifierKey(k));

            // If adding this key would exceed the limits (4 modifiers + 1 action key), don't add it and show notification
            if ((RemappingHelper.IsModifierKey(virtualKey) && modifierCount >= 4) ||
                (!RemappingHelper.IsModifierKey(virtualKey) && _currentlyPressedKeys.Count >= 5))
            {
                _activeTarget.OnInputLimitReached();
                return;
            }

            // Check if this is a different variant of a modifier key already pressed
            if (RemappingHelper.IsModifierKey(virtualKey))
            {
                // Remove existing variant of this modifier key if a new one is pressed
                // This is to ensure that only one variant of a modifier key is displayed at a time
                RemoveExistingModifierVariant(virtualKey);
            }

            if (_currentlyPressedKeys.Add(virtualKey))
            {
                _keyPressOrder.Add(virtualKey);

                // Notify the target page
                _activeTarget.OnKeyDown(virtualKey, GetFormattedKeyList());
            }
        }

        private void KeyUp(int key)
        {
            if (_activeTarget == null)
            {
                return;
            }

            VirtualKey virtualKey = (VirtualKey)key;

            if (_currentlyPressedKeys.Remove(virtualKey))
            {
                _keyPressOrder.Remove(virtualKey);

                _activeTarget.OnKeyUp(virtualKey, GetFormattedKeyList());
            }
        }

        // Display the modifier keys and the action key in order, e.g. "Ctrl + Alt + A"
        private List<string> GetFormattedKeyList()
        {
            if (_activeTarget == null)
            {
                return new List<string>();
            }

            List<string> keyList = new List<string>();
            List<VirtualKey> modifierKeys = new List<VirtualKey>();
            VirtualKey? actionKey = null;

            foreach (var key in _keyPressOrder)
            {
                if (!_currentlyPressedKeys.Contains(key))
                {
                    continue;
                }

                if (RemappingHelper.IsModifierKey(key))
                {
                    if (!modifierKeys.Contains(key))
                    {
                        modifierKeys.Add(key);
                    }
                }
                else
                {
                    actionKey = key;
                }
            }

            foreach (var key in modifierKeys)
            {
                keyList.Add(_mappingService.GetKeyDisplayName((int)key));
            }

            if (actionKey.HasValue)
            {
                keyList.Add(_mappingService.GetKeyDisplayName((int)actionKey.Value));
            }

            return keyList;
        }

        private void RemoveExistingModifierVariant(VirtualKey key)
        {
            KeyType keyType = (KeyType)KeyboardManagerInterop.GetKeyType((int)key);

            // No need to remove if the key is an action key
            if (keyType == KeyType.Action)
            {
                return;
            }

            foreach (var existingKey in _currentlyPressedKeys.ToList())
            {
                if (existingKey != key)
                {
                    KeyType existingKeyType = (KeyType)KeyboardManagerInterop.GetKeyType((int)existingKey);

                    // Remove the existing key if it is a modifier key and has the same type as the new key
                    if (existingKeyType == keyType)
                    {
                        _currentlyPressedKeys.Remove(existingKey);
                        _keyPressOrder.Remove(existingKey);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CleanupHook();
                    _mappingService?.Dispose();
                }

                _disposed = true;
            }
        }
    }

    public interface IKeyboardHookTarget
    {
        void OnKeyDown(VirtualKey key, List<string> formattedKeys);

        void OnKeyUp(VirtualKey key, List<string> formattedKeys)
        {
        }

        void ClearKeys();

        void OnInputLimitReached();
    }
}
