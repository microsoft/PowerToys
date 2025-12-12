// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CmdPalKeyboardService;
using Microsoft.CommandPalette.UI.Models;
using Microsoft.Extensions.Logging;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CommandPalette.UI.Services;

public partial class KeyboardService : IDisposable
{
    private readonly KeyboardListener _keyboardListener;
    private readonly SettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly List<CommandHotKey> _hotkeys = [];

    private HWND _hwnd;
    private bool _disposed;
    private UnhookWindowsHookExSafeHandle? _handle;
    private HOOKPROC? _hookProc; // Keep reference to prevent GC collection

    /// <summary>
    /// Event that is raised when a key is pressed down.
    /// </summary>
    public event EventHandler<KeyPressedEventArgs>? KeyPressed;

    public KeyboardService(SettingsService settingsService, ILogger logger)
    {
        _logger = logger;
        _settingsService = settingsService;
        _keyboardListener = new KeyboardListener();
        _keyboardListener.Start();

        StartListening();

        _settingsService.SettingsChanged += SettingsChanged;
    }

    public void SetProcessCommand(HWND hwnd, ProcessCommand processCommand)
    {
        _hwnd = hwnd;
        _keyboardListener.SetProcessCommand(processCommand);
        SetupHotkeys();
    }

    private void SettingsChanged(SettingsModel sender, object? e)
    {
        SetupHotkeys();
    }

    private void UnregisterHotkeys()
    {
        _keyboardListener.ClearHotkeys();

        while (_hotkeys.Count > 0)
        {
            PInvoke.UnregisterHotKey(_hwnd, _hotkeys.Count - 1);
            _hotkeys.RemoveAt(_hotkeys.Count - 1);
        }
    }

    private void SetupHotkeys()
    {
        UnregisterHotkeys();

        var globalHotkey = _settingsService.CurrentSettings.Hotkey;
        if (globalHotkey is not null)
        {
            if (_settingsService.CurrentSettings.UseLowLevelGlobalHotkey)
            {
                _keyboardListener.SetHotkeyAction(globalHotkey.Win, globalHotkey.Ctrl, globalHotkey.Shift, globalHotkey.Alt, (byte)globalHotkey.Code, string.Empty);
                _hotkeys.Add(new(globalHotkey, string.Empty));
            }
            else
            {
                var vk = globalHotkey.Code;
                var modifiers =
                                (globalHotkey.Alt ? HOT_KEY_MODIFIERS.MOD_ALT : 0) |
                                (globalHotkey.Ctrl ? HOT_KEY_MODIFIERS.MOD_CONTROL : 0) |
                                (globalHotkey.Shift ? HOT_KEY_MODIFIERS.MOD_SHIFT : 0) |
                                (globalHotkey.Win ? HOT_KEY_MODIFIERS.MOD_WIN : 0)
                                ;

                var success = PInvoke.RegisterHotKey(_hwnd, _hotkeys.Count, modifiers, (uint)vk);
                if (success)
                {
                    _hotkeys.Add(new(globalHotkey, string.Empty));
                }
            }
        }

        foreach (var commandHotkey in _settingsService.CurrentSettings.CommandHotkeys)
        {
            var key = commandHotkey.Hotkey;
            if (key is not null)
            {
                if (_settingsService.CurrentSettings.UseLowLevelGlobalHotkey)
                {
                    _keyboardListener.SetHotkeyAction(key.Win, key.Ctrl, key.Shift, key.Alt, (byte)key.Code, commandHotkey.CommandId);
                    _hotkeys.Add(new(commandHotkey.Hotkey, commandHotkey.CommandId));
                }
                else
                {
                    var vk = key.Code;
                    var modifiers =
                        (key.Alt ? HOT_KEY_MODIFIERS.MOD_ALT : 0) |
                        (key.Ctrl ? HOT_KEY_MODIFIERS.MOD_CONTROL : 0) |
                        (key.Shift ? HOT_KEY_MODIFIERS.MOD_SHIFT : 0) |
                        (key.Win ? HOT_KEY_MODIFIERS.MOD_WIN : 0)
                        ;
                    var success = PInvoke.RegisterHotKey(_hwnd, _hotkeys.Count, modifiers, (uint)vk);
                    if (success)
                    {
                        _hotkeys.Add(commandHotkey);
                    }
                }
            }
        }
    }

    private bool StartListening()
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            RegisterKeyboardHook();
            return true;
        }
        catch (Exception ex)
        {
            Log_FailedToRegisterHook(ex);
            return false;
        }
    }

    private void RegisterKeyboardHook()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_handle is not null && !_handle.IsInvalid)
        {
            // Hook is already set
            return;
        }

        _hookProc = KeyEventHook;
        if (!SetWindowKeyHook(_hookProc))
        {
            throw new InvalidOperationException("Failed to register keyboard hook.");
        }
    }

    private bool SetWindowKeyHook(HOOKPROC hookProc)
    {
        if (_handle is not null && !_handle.IsInvalid)
        {
            // Hook is already set
            return false;
        }

        _handle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_KEYBOARD,
            hookProc,
            PInvoke.GetModuleHandle(null),
            PInvoke.GetCurrentThreadId());

        // Check if the hook was successfully set
        return _handle is not null && !_handle.IsInvalid;
    }

    private static bool IsKeyDownHook(LPARAM lParam)
    {
        // The 30th bit tells what the previous key state is with 0 being the "UP" state
        // For more info see https://learn.microsoft.com/windows/win32/winmsg/keyboardproc#lparam-in
        return ((lParam.Value >> 30) & 1) == 0;
    }

    private LRESULT KeyEventHook(int nCode, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (nCode >= 0 && IsKeyDownHook(lParam))
            {
                InvokeKeyDown((VirtualKey)wParam.Value);
            }
        }
        catch (Exception ex)
        {
            Log_ErrorInvokingKeyDownHook(ex);
        }

        // Call next hook in chain - pass null as first parameter for current hook
        return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
    }

    private void InvokeKeyDown(VirtualKey virtualKey)
    {
        if (!_disposed)
        {
            KeyPressed?.Invoke(this, new KeyPressedEventArgs(virtualKey));
        }
    }

    public void Dispose()
    {
        UnregisterHotkeys();

        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged -= SettingsChanged;
        }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void UnregisterKeyboardHook()
    {
        if (_handle is not null && !_handle.IsInvalid)
        {
            // The SafeHandle should automatically call UnhookWindowsHookEx when disposed
            _handle.Dispose();
            _handle = null;
        }

        _hookProc = null;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                UnregisterKeyboardHook();
            }

            _disposed = true;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to register hook")]
    partial void Log_FailedToRegisterHook(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed when invoking key down keyboard hook event")]
    partial void Log_ErrorInvokingKeyDownHook(Exception ex);
}
