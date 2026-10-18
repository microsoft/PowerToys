// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class GlobalKeyboardListener : IDisposable
{
    private readonly Action<string> _processCommand;
    private readonly Action<Exception> _reportError;
    private readonly IGlobalKeyboardHook _hook;
    private readonly HOOKPROC _hookProc; // Keep the callback alive while the hook is installed.
    private readonly object _hotkeysLock = new();
    private readonly Dictionary<Hotkey, string> _hotkeys = [];
    private IDisposable? _handle;
    private bool _disposed;

    public GlobalKeyboardListener(Action<string> processCommand, Action<Exception> reportError, IGlobalKeyboardHook? hook = null)
    {
        ArgumentNullException.ThrowIfNull(processCommand);
        ArgumentNullException.ThrowIfNull(reportError);
        _processCommand = processCommand;
        _reportError = reportError;
        _hook = hook ?? new GlobalKeyboardHook();
        _hookProc = KeyboardHook;
    }

    public bool Start([NotNullWhen(false)] out Exception? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        error = null;

        if (_handle is not null)
        {
            return true;
        }

        try
        {
            // The callback runs on this thread, which must keep pumping messages.
            _handle = _hook.Install(_hookProc);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    public void Stop()
    {
        _handle?.Dispose();
        _handle = null;
    }

    public void SetHotkeyAction(bool win, bool ctrl, bool shift, bool alt, byte key, string commandId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(commandId);

        lock (_hotkeysLock)
        {
            // Preserve the first registration when multiple commands share a shortcut.
            _hotkeys.TryAdd(new(win, ctrl, shift, alt, key), commandId);
        }
    }

    public void ClearHotkeys()
    {
        lock (_hotkeysLock)
        {
            _hotkeys.Clear();
        }
    }

    private string? GetHotkeyCommand(bool win, bool ctrl, bool shift, bool alt, byte key)
    {
        var hotkey = new Hotkey(win, ctrl, shift, alt, key);
        if (hotkey == default)
        {
            return null;
        }

        lock (_hotkeysLock)
        {
            return _hotkeys.GetValueOrDefault(hotkey);
        }
    }

    private unsafe LRESULT KeyboardHook(int nCode, WPARAM wParam, LPARAM lParam)
    {
        var matched = false;
        try
        {
            if (!_disposed && nCode >= 0 && (wParam.Value == PInvoke.WM_KEYDOWN || wParam.Value == PInvoke.WM_SYSKEYDOWN))
            {
                var key = (byte)((KBDLLHOOKSTRUCT*)lParam.Value)->vkCode;
                if (GetHotkeyCommand(
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_LWIN) || _hook.IsKeyDown(VIRTUAL_KEY.VK_RWIN),
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_CONTROL),
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_SHIFT),
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_MENU),
                    key) is { } commandId)
                {
                    matched = true;
                    try
                    {
                        _processCommand(commandId);
                    }
                    finally
                    {
                        // Mask modifier-only actions after every matched shortcut.
                        _hook.SendDummyKeyUp();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Defer reporting so a slow logger cannot block keyboard input.
            _ = Task.Run(() => _reportError(ex));
        }

        return matched ? (LRESULT)1 : _hook.CallNext(nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        ClearHotkeys();
    }

    private readonly record struct Hotkey(bool Win, bool Ctrl, bool Shift, bool Alt, byte Key);
}
