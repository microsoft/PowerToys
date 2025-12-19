// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using WinRT.Interop;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Service for handling hotkey registration in-process.
    /// Uses RegisterHotKey Win32 API instead of Runner's centralized mechanism
    /// to avoid IPC timing issues (CmdPal pattern).
    /// </summary>
    internal sealed partial class HotkeyService : IDisposable
    {
        private const int HotkeyId = 9001;

        private readonly ISettingsUtils _settingsUtils;
        private readonly Action _hotkeyAction;

        private nint _hwnd;
        private nint _originalWndProc;

        // Must keep delegate reference to prevent GC collection
        private WndProcDelegate? _hotkeyWndProc;
        private bool _isRegistered;
        private bool _disposed;

        public HotkeyService(ISettingsUtils settingsUtils, Action hotkeyAction)
        {
            _settingsUtils = settingsUtils;
            _hotkeyAction = hotkeyAction;
        }

        /// <summary>
        /// Initialize the hotkey service with a window handle.
        /// Must be called after window is created.
        /// </summary>
        /// <param name="window">The WinUI window to attach to.</param>
        public void Initialize(Microsoft.UI.Xaml.Window window)
        {
            _hwnd = WindowNative.GetWindowHandle(window);
            Logger.LogTrace($"[HotkeyService] Initialize: hwnd=0x{_hwnd:X}");

            // LOAD BEARING: If you don't stick the pointer to the WndProc into a
            // member (and instead use a local), then the pointer we marshal
            // into the WindowLongPtr will be useless after we leave this function,
            // and our WndProc will explode.
            _hotkeyWndProc = HotkeyWndProc;
            var wndProcPointer = Marshal.GetFunctionPointerForDelegate(_hotkeyWndProc);
            _originalWndProc = SetWindowLongPtrNative(_hwnd, GwlWndProc, wndProcPointer);

            Logger.LogTrace($"[HotkeyService] WndProc hooked, original=0x{_originalWndProc:X}");

            // Register hotkey based on current settings
            ReloadSettings();
        }

        /// <summary>
        /// Reload settings and re-register hotkey.
        /// Call this when settings change.
        /// </summary>
        public void ReloadSettings()
        {
            Logger.LogTrace("[HotkeyService] ReloadSettings called");
            UnregisterHotkey();

            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            var hotkey = settings?.Properties?.ActivationShortcut;

            if (hotkey == null || !hotkey.IsValid())
            {
                Logger.LogInfo("[HotkeyService] No valid hotkey configured");
                return;
            }

            RegisterHotkey(hotkey);
        }

        private void RegisterHotkey(HotkeySettings hotkey)
        {
            if (_hwnd == 0)
            {
                Logger.LogWarning("[HotkeyService] Cannot register hotkey: window handle not set");
                return;
            }

            // Build modifiers using bit flags
            uint modifiers = ModNoRepeat
                | (hotkey.Win ? ModWin : 0)
                | (hotkey.Ctrl ? ModControl : 0)
                | (hotkey.Alt ? ModAlt : 0)
                | (hotkey.Shift ? ModShift : 0);

            if (RegisterHotKeyNative(_hwnd, HotkeyId, modifiers, (uint)hotkey.Code))
            {
                _isRegistered = true;
                Logger.LogInfo($"[HotkeyService] Hotkey registered: {hotkey}");
            }
            else
            {
                Logger.LogError($"[HotkeyService] Failed to register hotkey: {hotkey}, error={Marshal.GetLastWin32Error()}");
            }
        }

        private void UnregisterHotkey()
        {
            if (!_isRegistered || _hwnd == 0)
            {
                return;
            }

            bool success = UnregisterHotKeyNative(_hwnd, HotkeyId);

            if (success)
            {
                Logger.LogTrace("[HotkeyService] Hotkey unregistered");
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                Logger.LogWarning($"[HotkeyService] Failed to unregister hotkey, error={error}");
            }

            _isRegistered = false;
        }

        private nint HotkeyWndProc(nint hwnd, uint uMsg, nuint wParam, nint lParam)
        {
            if (uMsg == WmHotkey && (int)wParam == HotkeyId)
            {
                Logger.LogInfo("[HotkeyService] WM_HOTKEY received, invoking action");
                try
                {
                    _hotkeyAction?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[HotkeyService] Hotkey action failed: {ex.Message}");
                }

                return 0;
            }

            return CallWindowProcNative(_originalWndProc, hwnd, uMsg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UnregisterHotkey();
            _disposed = true;
        }

        // P/Invoke constants
        private const int GwlWndProc = -4;
        private const uint WmHotkey = 0x0312;

        // HOT_KEY_MODIFIERS flags
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;
        private const uint ModNoRepeat = 0x4000;

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtrNative(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static partial nint CallWindowProcNative(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

        [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKeyNative(nint hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKeyNative(nint hWnd, int id);
    }
}
