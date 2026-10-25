// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCommon;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Dock;

// Thank you to https://stackoverflow.com/a/35422795/1481137
/// <summary>
/// Observes foreground-window changes across processes on the current desktop.
/// </summary>
/// <remarks>
/// Call <see cref="Start"/>, <see cref="Stop"/>, and <see cref="Dispose"/> on the constructing thread.
/// That thread must pump Windows messages while listening; dispose before its message loop ends.
/// Subscribing does not start the listener.
/// </remarks>
internal sealed partial class WindowsEventsListener : IDisposable
{
    /// <summary>
    /// Occurs when the foreground window changes while listening.
    /// </summary>
    /// <remarks>
    /// Handlers run synchronously on the owning thread; defer work that may pump messages.
    /// Handler exceptions do not propagate, but prevent later handlers from receiving that notification.
    /// </remarks>
    public event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundWindowChanged;

    private const string WorkerWWindowClassName = "WorkerW";
    private const string ProgmanWindowClassName = "Progman";

    private readonly uint _threadId = PInvoke.GetCurrentThreadId();
    private readonly Func<WINEVENTPROC, HWINEVENTHOOK> _setHook;
    private readonly Func<HWINEVENTHOOK, bool> _unhook;
    private GCHandle _hookProc;

    // Unhooking requires the installing thread, so a finalizing SafeHandle cannot own this hook.
    private HWINEVENTHOOK _hookHandle;
    private bool _isListening;
    private bool _disposed;

    /// <summary>
    /// Gets whether a hook is held, including after failed cleanup.
    /// </summary>
    public bool IsHooked => !_hookHandle.IsNull;

    /// <summary>
    /// Initializes a stopped listener owned by the current thread.
    /// </summary>
    public WindowsEventsListener()
        : this(
            static hookProc => PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, HMODULE.Null, hookProc, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT),
            static hookHandle => PInvoke.UnhookWinEvent(hookHandle))
    {
    }

    /// <summary>
    /// Initializes a stopped listener with the supplied hook operations.
    /// </summary>
    /// <param name="setHook">Installs the callback and returns its hook handle, or a null handle on failure.</param>
    /// <param name="unhook">Removes the supplied hook handle and returns whether removal succeeded.</param>
    internal WindowsEventsListener(Func<WINEVENTPROC, HWINEVENTHOOK> setHook, Func<HWINEVENTHOOK, bool> unhook)
    {
        _setHook = setHook;
        _unhook = unhook;
    }

    /// <summary>
    /// Starts listening for foreground-window changes.
    /// </summary>
    /// <returns><see langword="true"/> if listening started; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Idempotent while listening. Failed starts can be retried. No initial notification is raised.</remarks>
    /// <exception cref="InvalidOperationException">The caller is not on the owning thread.</exception>
    /// <exception cref="ObjectDisposedException">The listener has been disposed.</exception>
    public bool Start()
    {
        VerifyThreadAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsHooked)
        {
            _isListening = true;
            return true;
        }

        // The native function pointer does not keep this instance or its delegate alive.
        _hookProc = GCHandle.Alloc((WINEVENTPROC)WinEventCallback);
        try
        {
            _hookHandle = _setHook((WINEVENTPROC)_hookProc.Target!);
            _isListening = IsHooked;
            return _isListening;
        }
        finally
        {
            if (!IsHooked)
            {
                _hookProc.Free();
            }
        }
    }

    /// <summary>
    /// Stops notifications, preserving subscriptions for a later <see cref="Start"/>.
    /// </summary>
    /// <remarks>
    /// Cleanup failure leaves notifications stopped and <see cref="IsHooked"/> true.
    /// Repeat <see cref="Stop"/> or <see cref="Dispose"/> to retry.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The caller is not on the owning thread.</exception>
    public void Stop()
    {
        VerifyThreadAccess();
        _isListening = false;
        Unhook();
    }

    /// <summary>
    /// Stops notifications, clears subscriptions, and prevents restarting.
    /// </summary>
    /// <remarks>Repeat this call to retry failed cleanup.</remarks>
    /// <exception cref="InvalidOperationException">The caller is not on the owning thread.</exception>
    public void Dispose()
    {
        VerifyThreadAccess();
        _disposed = true;
        _isListening = false;
        ForegroundWindowChanged = null;
        Unhook();
    }

    private void Unhook()
    {
        if (!IsHooked)
        {
            return;
        }

        if (!_unhook(_hookHandle))
        {
            Logger.LogError("Failed to remove the desktop foreground hook.");
            return;
        }

        _hookHandle = default;
        _hookProc.Free();
    }

    private void VerifyThreadAccess()
    {
        if (PInvoke.GetCurrentThreadId() != _threadId)
        {
            throw new InvalidOperationException("The desktop hook must be accessed on its owning thread.");
        }
    }

    private static unsafe bool IsDesktopWindow(HWND hwnd)
    {
        const int maxLength = 32;
        var buffer = stackalloc char[maxLength];
        var className = new ReadOnlySpan<char>(buffer, PInvoke.GetClassName(hwnd, buffer, maxLength));
        return className.SequenceEqual(WorkerWWindowClassName) || className.SequenceEqual(ProgmanWindowClassName);
    }

    internal void WinEventCallback(
        HWINEVENTHOOK hWinEventHook,
        uint eventType,
        HWND hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        try
        {
            if (_isListening && eventType == PInvoke.EVENT_SYSTEM_FOREGROUND)
            {
                ForegroundWindowChanged?.Invoke(this, new ForegroundWindowChangedEventArgs(IsDesktopWindow(hwnd)));
            }
        }
        catch (Exception ex)
        {
            // Keep error reporting outside the native callback.
            _ = Task.Run(() => Logger.LogError("Failed to handle a desktop foreground event.", ex));
        }
    }
}
