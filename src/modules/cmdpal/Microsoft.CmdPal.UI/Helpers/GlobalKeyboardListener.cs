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
    // Matches the runner's hotkey handoff budget (#52257). Well under LowLevelHooksTimeout.
    internal static readonly TimeSpan DefaultCommandHandoffBudget = TimeSpan.FromMilliseconds(50);

    private readonly Action<string> _processCommand;
    private readonly Action<Exception> _reportError;
    private readonly Func<Action, bool> _dispatch;
    private readonly TimeSpan _commandHandoffBudget;
    private readonly IGlobalKeyboardHook _hook;
    private readonly HOOKPROC _hookProc; // Keep the callback alive while the hook is installed.
    private readonly object _hotkeysLock = new();
    private readonly Dictionary<Hotkey, string> _hotkeys = [];
    private string? _windowsKeyCommand;
    private int _windowsKeysDown;
    private byte _pendingWindowsKey;
    private IDisposable? _handle;
    private volatile bool _disposed;

    /// <param name="processCommand">Runs a matched shortcut's command. Always invoked through <paramref name="dispatch"/>.</param>
    /// <param name="reportError">Reports failures. Never invoked on the hook thread.</param>
    /// <param name="dispatch">Queues work to the thread that owns <paramref name="processCommand"/>, in FIFO order, without running it inline.</param>
    /// <param name="hook">The low-level keyboard hook to install.</param>
    /// <param name="commandHandoffBudget">How long the hook waits for a queued command before it releases the key press.</param>
    public GlobalKeyboardListener(
        Action<string> processCommand,
        Action<Exception> reportError,
        Func<Action, bool> dispatch,
        IGlobalKeyboardHook? hook = null,
        TimeSpan? commandHandoffBudget = null)
    {
        ArgumentNullException.ThrowIfNull(processCommand);
        ArgumentNullException.ThrowIfNull(reportError);
        ArgumentNullException.ThrowIfNull(dispatch);
        _processCommand = processCommand;
        _reportError = reportError;
        _dispatch = dispatch;
        _commandHandoffBudget = commandHandoffBudget ?? DefaultCommandHandoffBudget;
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
            // The hook runs on its own thread, so a busy UI thread cannot stall keyboard input.
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
        lock (_hotkeysLock)
        {
            _windowsKeysDown = 0;
            _pendingWindowsKey = 0;
        }
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
            _windowsKeyCommand = null;
            _windowsKeysDown = 0;
            _pendingWindowsKey = 0;
        }
    }

    public void SetWindowsKeyAction(string commandId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(commandId);
        lock (_hotkeysLock)
        {
            _windowsKeyCommand = commandId;
        }
    }

    private string? GetWindowsKeyCommand(byte key, bool isDown)
    {
        lock (_hotkeysLock)
        {
            if (_windowsKeyCommand is null)
            {
                return null;
            }

            if (key is not ((byte)VIRTUAL_KEY.VK_LWIN or (byte)VIRTUAL_KEY.VK_RWIN))
            {
                if (isDown && key != 0xFF)
                {
                    _pendingWindowsKey = 0;
                }

                return null;
            }

            var mask = key == (byte)VIRTUAL_KEY.VK_LWIN ? 1 : 2;
            if (isDown)
            {
                if ((_windowsKeysDown & mask) == 0)
                {
                    _pendingWindowsKey = _windowsKeysDown == 0 && !IsOtherKeyDown(key) ? key : (byte)0;
                    _windowsKeysDown |= mask;
                }

                return null;
            }

            _windowsKeysDown &= ~mask;
            var activate = _pendingWindowsKey == key && !IsOtherKeyDown(key);
            _pendingWindowsKey = 0;
            return activate ? _windowsKeyCommand : null;
        }
    }

    private bool IsOtherKeyDown(byte windowsKey)
    {
        // Include keys held before the hook saw the Windows-key press.
        for (var key = (int)VIRTUAL_KEY.VK_BACK; key < 0xFF; key++)
        {
            if (key != windowsKey && _hook.IsKeyDown((VIRTUAL_KEY)key))
            {
                return true;
            }
        }

        return false;
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

    // Runs on the hook thread, which stalls all keyboard input until it returns. Only the
    // swallow decision and the masking key-ups happen here. Commands run on the dispatch
    // thread. The key-ups stay here because they replace or follow the swallowed key press,
    // and sending them later could leave a modifier logically held.
    private unsafe LRESULT KeyboardHook(int nCode, WPARAM wParam, LPARAM lParam)
    {
        var matched = false;
        try
        {
            var isDown = wParam.Value == PInvoke.WM_KEYDOWN || wParam.Value == PInvoke.WM_SYSKEYDOWN;
            var isUp = wParam.Value == PInvoke.WM_KEYUP || wParam.Value == PInvoke.WM_SYSKEYUP;
            if (!_disposed && nCode >= 0 && (isDown || isUp))
            {
                var data = (KBDLLHOOKSTRUCT*)lParam.Value;
                var key = (byte)data->vkCode;
                if (GetWindowsKeyCommand(key, isDown) is { } windowsCommandId)
                {
                    _hook.SendWindowsKeyUp((VIRTUAL_KEY)key);
                    matched = true;
                    DispatchCommand(windowsCommandId);
                }
                else if (isDown && GetHotkeyCommand(
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_LWIN) || _hook.IsKeyDown(VIRTUAL_KEY.VK_RWIN),
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_CONTROL),
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_SHIFT),
                    _hook.IsKeyDown(VIRTUAL_KEY.VK_MENU),
                    key) is { } commandId)
                {
                    matched = true;
                    try
                    {
                        DispatchCommand(commandId);
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
            ReportErrorAsync(ex);
        }

        return matched ? (LRESULT)1 : _hook.CallNext(nCode, wParam, lParam);
    }

    private void DispatchCommand(string commandId)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatch(() => RunCommand(commandId, completed)))
        {
            throw new InvalidOperationException("Failed to queue the shortcut command.");
        }

        // Give a fast command time to finish while this key press is still being processed,
        // so it keeps the foreground activation rights of the key press. A slow command keeps
        // running on the dispatch thread after the budget instead of stalling input.
        _ = completed.Task.Wait(_commandHandoffBudget);
    }

    private void RunCommand(string commandId, TaskCompletionSource completed)
    {
        try
        {
            if (!_disposed)
            {
                _processCommand(commandId);
            }
        }
        catch (Exception ex)
        {
            ReportErrorAsync(ex);
        }
        finally
        {
            completed.TrySetResult();
        }
    }

    private void ReportErrorAsync(Exception error) => _ = Task.Run(() => _reportError(error));

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
