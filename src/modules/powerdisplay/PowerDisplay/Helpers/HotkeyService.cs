// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Service for handling hotkey registration in-process.
    /// Uses RegisterHotKey Win32 API instead of Runner's centralized mechanism
    /// to avoid IPC timing issues (CmdPal pattern).
    /// </summary>
    internal sealed partial class HotkeyService : IDisposable
    {
        private const int ToggleWindowHotkeyId = 9001;
        private const int IncreaseBrightnessHotkeyId = 9002;
        private const int DecreaseBrightnessHotkeyId = 9003;
        private const int IncreaseContrastHotkeyId = 9004;
        private const int DecreaseContrastHotkeyId = 9005;
        private const int IncreaseVolumeHotkeyId = 9006;
        private const int DecreaseVolumeHotkeyId = 9007;
        private const int IncreaseSdrContentBrightnessHotkeyId = 9008;
        private const int DecreaseSdrContentBrightnessHotkeyId = 9009;

        private readonly SettingsUtils _settingsUtils;
        private readonly Dictionary<int, Action> _hotkeyActions;
        private readonly HashSet<int> _registeredHotkeyIds = new();

        private nint _hwnd;
        private bool _disposed;

        public HotkeyService(
            SettingsUtils settingsUtils,
            Action toggleWindowAction,
            Action<PowerDisplayHotkeyAction> adjustmentAction)
        {
            _settingsUtils = settingsUtils;
            _hotkeyActions = new Dictionary<int, Action>
            {
                [ToggleWindowHotkeyId] = toggleWindowAction,
                [IncreaseBrightnessHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.IncreaseBrightness),
                [DecreaseBrightnessHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.DecreaseBrightness),
                [IncreaseContrastHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.IncreaseContrast),
                [DecreaseContrastHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.DecreaseContrast),
                [IncreaseVolumeHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.IncreaseVolume),
                [DecreaseVolumeHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.DecreaseVolume),
                [IncreaseSdrContentBrightnessHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.IncreaseSdrContentBrightness),
                [DecreaseSdrContentBrightnessHotkeyId] = () => adjustmentAction(PowerDisplayHotkeyAction.DecreaseSdrContentBrightness),
            };
        }

        /// <summary>
        /// Initialize the hotkey service with a window handle.
        /// Must be called after window is created and WndProcService is attached.
        /// </summary>
        public void Initialize(nint hwnd)
        {
            _hwnd = hwnd;
            ReloadSettings();
        }

        /// <summary>
        /// Handle WM_HOTKEY messages. Called by WndProcService.
        /// </summary>
        /// <returns>True if the message was handled.</returns>
        public bool HandleMessage(uint uMsg, nuint wParam)
        {
            var hotkeyId = (int)wParam;
            if (uMsg != WmHotkey ||
                !_registeredHotkeyIds.Contains(hotkeyId) ||
                !_hotkeyActions.TryGetValue(hotkeyId, out var action))
            {
                return false;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[HotkeyService] Hotkey action failed: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// Reload settings and re-register all configured hotkeys.
        /// Call this when settings change.
        /// </summary>
        public void ReloadSettings()
        {
            UnregisterHotkeys();

            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            if (settings?.Properties == null)
            {
                return;
            }

            RegisterHotkey(ToggleWindowHotkeyId, settings.Properties.ActivationShortcut);
            RegisterHotkey(IncreaseBrightnessHotkeyId, settings.Properties.IncreaseBrightnessShortcut);
            RegisterHotkey(DecreaseBrightnessHotkeyId, settings.Properties.DecreaseBrightnessShortcut);
            RegisterHotkey(IncreaseContrastHotkeyId, settings.Properties.IncreaseContrastShortcut);
            RegisterHotkey(DecreaseContrastHotkeyId, settings.Properties.DecreaseContrastShortcut);
            RegisterHotkey(IncreaseVolumeHotkeyId, settings.Properties.IncreaseVolumeShortcut);
            RegisterHotkey(DecreaseVolumeHotkeyId, settings.Properties.DecreaseVolumeShortcut);
            RegisterHotkey(IncreaseSdrContentBrightnessHotkeyId, settings.Properties.IncreaseSdrContentBrightnessShortcut);
            RegisterHotkey(DecreaseSdrContentBrightnessHotkeyId, settings.Properties.DecreaseSdrContentBrightnessShortcut);
        }

        private void RegisterHotkey(int hotkeyId, HotkeySettings? hotkey)
        {
            if (_hwnd == 0 || hotkey == null || !hotkey.IsValid())
            {
                return;
            }

            // Build modifiers using bit flags
            uint modifiers = ModNoRepeat
                | (hotkey.Win ? ModWin : 0)
                | (hotkey.Ctrl ? ModControl : 0)
                | (hotkey.Alt ? ModAlt : 0)
                | (hotkey.Shift ? ModShift : 0);

            if (RegisterHotKeyNative(_hwnd, hotkeyId, modifiers, (uint)hotkey.Code))
            {
                _registeredHotkeyIds.Add(hotkeyId);
            }
            else
            {
                Logger.LogError(
                    $"[HotkeyService] Failed to register hotkey id={hotkeyId}: {hotkey}, error={Marshal.GetLastWin32Error()}");
            }
        }

        private void UnregisterHotkeys()
        {
            if (_hwnd == 0)
            {
                return;
            }

            foreach (var hotkeyId in _registeredHotkeyIds)
            {
                if (!UnregisterHotKeyNative(_hwnd, hotkeyId))
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.LogWarning(
                        $"[HotkeyService] Failed to unregister hotkey id={hotkeyId}, error={error}");
                }
            }

            _registeredHotkeyIds.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UnregisterHotkeys();
            _disposed = true;
        }

        // P/Invoke constants
        private const uint WmHotkey = 0x0312;

        // HOT_KEY_MODIFIERS flags
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;
        private const uint ModNoRepeat = 0x4000;

        [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKeyNative(nint hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKeyNative(nint hWnd, int id);
    }
}
