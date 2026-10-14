// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using KeyboardManagerEditorUI.Interop;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.System;

namespace KeyboardManagerEditorUI.Helpers
{
    public class KeyboardHookHelper : IDisposable
    {
        private static KeyboardHookHelper? _instance;

        private readonly KeyboardRecordingState _recordingState = new();

        public static KeyboardHookHelper Instance => _instance ??= new KeyboardHookHelper();

        private KeyboardMappingService? _mappingService;

        private HotkeySettingsControlHook? _keyboardHook;

        private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

        // The active page using this keyboard hook
        private IKeyboardHookTarget? _activeTarget;

        private HashSet<VirtualKey> _currentlyPressedKeys = new();
        private List<VirtualKey> _keyPressOrder = new();

        private bool _disposed;

        private bool _hasCapturedKeys;

        private bool _hasCapturedNonShiftKeys;

        private bool _restorePreviousKeys;

        private bool _moveFocusBackward;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, NativeKeyboardHelper.INPUT[] inputs, int size);

        // Singleton to make sure only one instance of the hook is active
        private KeyboardHookHelper()
        {
            try
            {
                _mappingService = new KeyboardMappingService();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Native KBM library unavailable for keyboard hook: {ex.Message}");
            }
        }

        public void ActivateHook(IKeyboardHookTarget target)
        {
            StopRecording(notifyTarget: false);

            _activeTarget = target;
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _recordingState.StartRecording();

            try
            {
                if (_keyboardHook == null)
                {
                    // Dispatch in the filter so obsolete tagged input can be swallowed without
                    // reaching KeyDown in a newer recording. The wrapper callbacks do no work.
                    _keyboardHook = new HotkeySettingsControlHook(
                        _ => { },
                        _ => { },
                        () => _recordingState.NeedsHook,
                        ShouldHandleKeyboardEvent);
                }
            }
            catch (Exception ex)
            {
                StopRecording(notifyTarget: false);
                QueueCleanup();
                Logger.LogWarning($"Keyboard hook unavailable: {ex.Message}");
            }
        }

        public void CleanupHook()
        {
            StopRecording();
            QueueCleanup();
        }

        private void StopRecording(bool restorePreviousKeys = false, bool notifyTarget = true, bool moveFocusBackward = false)
        {
            IKeyboardHookTarget? target = _activeTarget;
            _activeTarget = null;
            _recordingState.StopRecording();
            _hasCapturedKeys = false;
            _hasCapturedNonShiftKeys = false;

            _currentlyPressedKeys.Clear();
            _keyPressOrder.Clear();

            if (notifyTarget)
            {
                target?.OnRecordingStopped(restorePreviousKeys, moveFocusBackward);
            }
        }

        private static bool IsEditorForeground()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return false;
            }

            _ = GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
            return foregroundProcessId == (uint)Environment.ProcessId;
        }

        private bool ShouldHandleKeyboardEvent(int key, UIntPtr extraInfo, bool isKeyDown)
        {
            var virtualKey = (VirtualKey)key;
            if (_recordingState.TryCompleteRestoration(extraInfo.ToUInt64(), _currentlyPressedKeys, out bool suppressRestoration))
            {
                QueueCleanup();
                return suppressRestoration;
            }

            if (_activeTarget == null)
            {
                return false;
            }

            if (_recordingState.ExitRequested)
            {
                if (!isKeyDown)
                {
                    _currentlyPressedKeys.Remove(virtualKey);
                }

                return false;
            }

            // Pending input from an older recording can keep the hook alive in the background.
            // Only tagged input and releases during exit are handled regardless of foreground.
            if (!IsEditorForeground())
            {
                return false;
            }

            if (!isKeyDown && !_currentlyPressedKeys.Contains(virtualKey))
            {
                // This press predates recording (for example, Tab held while activating with Space).
                // Let its release update the system state without ending or changing this capture.
                return false;
            }

            if (isKeyDown && IsKeyboardNavigationExit(virtualKey))
            {
                // Shift was already swallowed by this hook, so the window would see a plain Tab and move
                // focus forward. Swallow the Tab instead and let the target move focus backward.
                bool moveFocusBackward = _currentlyPressedKeys.Count > 0;

                // Keep the recorded keys unless nothing was captured, or the only captured key is the
                // Shift held for Shift+Tab.
                _restorePreviousKeys = !_hasCapturedKeys || (moveFocusBackward && !_hasCapturedNonShiftKeys);
                _moveFocusBackward = moveFocusBackward;

                _recordingState.RequestExit();
                if (moveFocusBackward)
                {
                    RestoreShiftKeyState();
                }

                QueueCleanup();

                return moveFocusBackward;
            }

            if (isKeyDown)
            {
                KeyDown(key);
            }
            else
            {
                KeyUp(key);
            }

            return true;
        }

        private bool IsKeyboardNavigationExit(VirtualKey virtualKey)
        {
            return virtualKey == VirtualKey.Tab && _currentlyPressedKeys.All(IsShiftKey);
        }

        private static bool IsShiftKey(VirtualKey key)
        {
            return key == VirtualKey.Shift || key == VirtualKey.LeftShift || key == VirtualKey.RightShift;
        }

        private void RestoreShiftKeyState()
        {
            // The hook swallowed these Shift presses. Hand them back to the system so holding
            // Shift continues backward navigation. Keep observing releases until these tagged
            // events arrive, so a release cannot be followed by a stale injected key-down.
            var inputs = _currentlyPressedKeys.Where(IsShiftKey).Select(key => new NativeKeyboardHelper.INPUT
            {
                type = NativeKeyboardHelper.INPUTTYPE.INPUT_KEYBOARD,
                data = new NativeKeyboardHelper.InputUnion
                {
                    ki = new NativeKeyboardHelper.KEYBDINPUT
                    {
                        wVk = (short)key,
                        dwExtraInfo = (UIntPtr)_recordingState.AddRestoration(key),
                    },
                },
            }).ToArray();

            uint sent = SendInput((uint)inputs.Length, inputs, NativeKeyboardHelper.INPUT.Size);
            if (sent != inputs.Length)
            {
                foreach (var input in inputs.Skip((int)sent))
                {
                    _recordingState.RemoveUnsentRestoration(input.data.ki.dwExtraInfo.ToUInt64());
                }

                Logger.LogWarning($"Unable to restore Shift after recording: {Marshal.GetLastWin32Error()}");
            }
        }

        private void QueueCleanup()
        {
            int generation = _recordingState.Generation;
            if (_keyboardHook == null || (_recordingState.NeedsHook && !_recordingState.CanCompleteExit(generation)))
            {
                return;
            }

            if (_dispatcherQueue == null || !_dispatcherQueue.TryEnqueue(() =>
            {
                if (_recordingState.CanCompleteExit(generation))
                {
                    StopRecording(_restorePreviousKeys, moveFocusBackward: _moveFocusBackward);
                }

                // Recheck after notifying the target: it can start another recording. Native
                // disposal always runs here, outside the filter and any target callback it invokes.
                if (!_recordingState.NeedsHook)
                {
                    _keyboardHook?.Dispose();
                    _keyboardHook = null;
                }
            }))
            {
                // Never dispose the native hook from inside its filter when the dispatcher is shutting down.
                Logger.LogWarning("Unable to queue keyboard recording cleanup");
            }
        }

        private void KeyDown(int key)
        {
            if (_activeTarget == null || _recordingState.ExitRequested)
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
                _hasCapturedNonShiftKeys = false;
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
                _hasCapturedKeys = true;
                _hasCapturedNonShiftKeys |= !IsShiftKey(virtualKey);
                _keyPressOrder.Add(virtualKey);

                // When building chords, cap at 2 action keys: if a third action key arrives,
                // remove the oldest (shift behavior matching old editor).
                if (_activeTarget.AllowChords && !RemappingHelper.IsModifierKey(virtualKey))
                {
                    var actionKeysInOrder = _keyPressOrder.Where(k => !RemappingHelper.IsModifierKey(k)).ToList();
                    if (actionKeysInOrder.Count > 2)
                    {
                        _keyPressOrder.Remove(actionKeysInOrder[0]);
                    }
                }

                // Notify the target page
                _activeTarget.OnKeyDown(virtualKey, GetFormattedKeyList());
            }
        }

        private void KeyUp(int key)
        {
            if (_activeTarget == null || _recordingState.ExitRequested)
            {
                return;
            }

            VirtualKey virtualKey = (VirtualKey)key;

            if (_currentlyPressedKeys.Remove(virtualKey))
            {
                // When building chords, preserve released action keys in _keyPressOrder
                // so the next action key press is recognized as the chord's second key.
                // Only modifier releases clear from _keyPressOrder (matching old editor behavior).
                if (!_activeTarget.AllowChords || RemappingHelper.IsModifierKey(virtualKey))
                {
                    _keyPressOrder.Remove(virtualKey);
                }

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

            if (_mappingService is null)
            {
                return new List<string>();
            }

            List<string> keyList = new List<string>();
            List<VirtualKey> modifierKeys = new List<VirtualKey>();
            VirtualKey? actionKey = null;
            VirtualKey? actionKeyChord = null;

            foreach (var key in _keyPressOrder)
            {
                // For modifiers, only include if currently pressed.
                // For action keys when building chords, also include released keys
                // so the chord's first key stays visible while waiting for the second.
                if (!_currentlyPressedKeys.Contains(key))
                {
                    if (RemappingHelper.IsModifierKey(key) || !_activeTarget.AllowChords)
                    {
                        continue;
                    }
                }

                if (RemappingHelper.IsModifierKey(key))
                {
                    if (!modifierKeys.Contains(key))
                    {
                        modifierKeys.Add(key);
                    }
                }
                else if (actionKey.HasValue && _activeTarget.AllowChords)
                {
                    actionKeyChord = key;
                }
                else
                {
                    actionKey = key;
                }
            }

            // Sort modifier keys by the standard display order: Win → Ctrl → Alt → Shift
            // This matches the old C++ GetKeyVector behavior where keys are always shown in
            // canonical order regardless of the order the user pressed them.
            RemappingHelper.SortModifierKeys(modifierKeys);

            foreach (var key in modifierKeys)
            {
                keyList.Add(_mappingService.GetKeyDisplayName((int)key));
            }

            if (actionKey.HasValue)
            {
                keyList.Add(_mappingService.GetKeyDisplayName((int)actionKey.Value));
            }

            if (actionKeyChord.HasValue && _activeTarget.AllowChords)
            {
                keyList.Add(_mappingService.GetKeyDisplayName((int)actionKeyChord.Value));
            }

            return keyList;
        }

        private void RemoveExistingModifierVariant(VirtualKey key)
        {
            if (_mappingService is null)
            {
                return;
            }

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
        bool AllowChords { get; }

        void OnKeyDown(VirtualKey key, List<string> formattedKeys);

        void OnKeyUp(VirtualKey key, List<string> formattedKeys)
        {
        }

        void ClearKeys();

        void OnInputLimitReached();

        void OnRecordingStopped(bool restorePreviousKeys, bool moveFocusBackward);
    }
}
