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
            Logger.LogError("Failed when invoking key down keyboard hook event", ex);
        }

        // Call next hook in chain - pass null as first parameter for current hook
        return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
    }

    private void InvokeKeyDown(VirtualKey virtualKey)
    {
        if (!_disposed)
        {
            KeyPressed?.Invoke(this, new LocalKeyboardListenerKeyPressedEventArgs(virtualKey));
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
