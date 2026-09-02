// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// A class that listens for local keyboard events using a Windows hook.
/// </summary>
internal sealed partial class LocalKeyboardListener : IDisposable
{
    /// <summary>
    /// Event that is raised when a key is pressed down.
    /// </summary>
    public event EventHandler<LocalKeyboardListenerKeyPressedEventArgs>? KeyPressed;

    /// <summary>
    /// Event that is raised when a key changes between its pressed and released states.
    /// </summary>
    public event EventHandler<LocalKeyboardListenerKeyStateChangedEventArgs>? KeyStateChanged;

    /// <summary>
    /// Gets or sets a value indicating whether keyboard events are raised.
    /// </summary>
    public bool EnableRaisingEvents
    {
        get => _enableRaisingEvents;
        set
        {
            if (_enableRaisingEvents && !value)
            {
                _suppressedKeys.Clear();
            }

            _enableRaisingEvents = value;
        }
    }

    private readonly HashSet<VirtualKey> _suppressedKeys = [];

    private bool _enableRaisingEvents;
    private bool _disposed;
    private UnhookWindowsHookExSafeHandle? _handle;
    private HOOKPROC? _hookProc; // Keep reference to prevent GC collection

    /// <summary>
    /// Registers a global keyboard hook to listen for key down events.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Throws if the hook could not be registered, which may happen if the system is unable to set the hook.
    /// </exception>
    public void RegisterKeyboardHook()
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

    /// <summary>
    /// Attempts to register a global keyboard hook to listen for key down events.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the keyboard hook was successfully registered; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Start()
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
            Logger.LogError("Failed to register hook", ex);
            return false;
        }
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

    private static bool IsKeyUpHook(LPARAM lParam)
    {
        // The 31st bit is 1 when the key is being released.
        // For more info see https://learn.microsoft.com/windows/win32/winmsg/keyboardproc#lparam-in
        return ((lParam.Value >> 31) & 1) != 0;
    }

    private LRESULT KeyEventHook(int nCode, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var virtualKey = (VirtualKey)wParam.Value;
                if (IsKeyDownHook(lParam))
                {
                    if (EnableRaisingEvents && InvokeKeyDown(virtualKey))
                    {
                        if (EnableRaisingEvents)
                        {
                            _suppressedKeys.Add(virtualKey);
                        }

                        return (LRESULT)1;
                    }
                }
                else if (IsKeyUpHook(lParam))
                {
                    if (EnableRaisingEvents)
                    {
                        InvokeKeyUp(virtualKey);
                    }

                    if (_suppressedKeys.Remove(virtualKey))
                    {
                        return (LRESULT)1;
                    }
                }
                else if (_suppressedKeys.Contains(virtualKey))
                {
                    return (LRESULT)1;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed when invoking keyboard hook event", ex);
        }

        // Call next hook in chain - pass null as first parameter for current hook
        return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
    }

    private bool InvokeKeyDown(VirtualKey virtualKey)
    {
        if (_disposed)
        {
            return false;
        }

        KeyPressed?.Invoke(this, new LocalKeyboardListenerKeyPressedEventArgs(virtualKey));
        var args = new LocalKeyboardListenerKeyStateChangedEventArgs(virtualKey, true);
        KeyStateChanged?.Invoke(this, args);
        return args.Handled;
    }

    private void InvokeKeyUp(VirtualKey virtualKey)
    {
        if (!_disposed)
        {
            KeyStateChanged?.Invoke(this, new LocalKeyboardListenerKeyStateChangedEventArgs(virtualKey, false));
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
                UnregisterKeyboardHook();
            }

            _disposed = true;
        }
    }
}
