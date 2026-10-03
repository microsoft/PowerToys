// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.ViewModels;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Matches global shortcuts on a dedicated thread and dispatches commands to the UI.</summary>
/// <remarks>
/// The UI thread owns configuration and lifetime.
/// The hook thread runs above normal priority and reads immutable hotkey snapshots without locks.
/// Queued commands retain their input timestamp and snapshot; changing mappings or stopping cancels stale work.
/// Failed renewal preserves the current registration until a replacement succeeds.
/// </remarks>
internal sealed partial class GlobalKeyboardListener : IGlobalKeyboardListener
{
    private readonly Func<Action, bool> _tryEnqueue;
    private readonly Action<string, long> _processCommand;
    private readonly Action<Exception> _reportError;
    private readonly Action<Exception> _hookFailed;
    private readonly IGlobalKeyboardHook _hook;
    private readonly Func<long> _getTimestamp;
    private readonly HOOKPROC _hookProc; // Keep the callback alive until the hook thread exits.
    private readonly ManualResetEvent _stop = new(false);

    private string?[]?[] _hotkeys = new string?[256][];
    private string?[]?[]? _activeHotkeys;
    private Thread? _thread;
    private Exception? _startupError;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="GlobalKeyboardListener"/> class.</summary>
    /// <param name="tryEnqueue">Queues work asynchronously on the UI thread and returns whether it was accepted.</param>
    /// <param name="processCommand">Runs a command on the UI thread with its original input timestamp in Stopwatch ticks.</param>
    /// <param name="hookFailed">Runs on the UI thread after a fatal message-loop failure and hook removal.</param>
    /// <param name="reportError">Reports command, callback, dispatch, or renewal errors; must be safe to call from any thread.</param>
    /// <param name="hook">Native hook operations, or <see langword="null"/> to use Win32.</param>
    /// <param name="getTimestamp">Timestamp source in Stopwatch ticks, or <see langword="null"/> to use <see cref="Stopwatch.GetTimestamp"/>.</param>
    public GlobalKeyboardListener(
        Func<Action, bool> tryEnqueue,
        Action<string, long> processCommand,
        Action<Exception> hookFailed,
        Action<Exception> reportError,
        IGlobalKeyboardHook? hook = null,
        Func<long>? getTimestamp = null)
    {
        ArgumentNullException.ThrowIfNull(tryEnqueue);
        ArgumentNullException.ThrowIfNull(processCommand);
        ArgumentNullException.ThrowIfNull(hookFailed);
        ArgumentNullException.ThrowIfNull(reportError);
        _tryEnqueue = tryEnqueue;
        _processCommand = processCommand;
        _hookFailed = hookFailed;
        _reportError = reportError;
        _hook = hook ?? new GlobalKeyboardHook();
        _getTimestamp = getTimestamp ?? Stopwatch.GetTimestamp;
        _hookProc = KeyboardHook;
    }

    /// <inheritdoc/>
    public bool Start(out Exception? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        error = null;
        if (Volatile.Read(ref _activeHotkeys) is not null)
        {
            return true;
        }

        Stop();
        _stop.Reset();
        _startupError = null;
        using var ready = new ManualResetEventSlim();
        _hotkeys = (string?[]?[])_hotkeys.Clone();
        _thread = new Thread(() => Run(ready))
        {
            IsBackground = true,
            Name = "Command Palette global keyboard hook",
        };

        try
        {
            _thread.Start();
        }
        catch (Exception ex)
        {
            _thread = null;
            error = ex;
            return false;
        }

        ready.Wait();
        error = _startupError;
        if (error is not null)
        {
            Stop();
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public void Stop()
    {
        Volatile.Write(ref _activeHotkeys, null);

        if (_thread is not null)
        {
            _stop.Set();
            _thread.Join();
            _thread = null;
        }
    }

    /// <inheritdoc/>
    public bool SetHotkeys(IEnumerable<TopLevelHotkey> hotkeys)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string?[]?[] snapshot = new string?[256][];
        var hasHotkeys = false;
        foreach (var registration in hotkeys)
        {
            var key = registration.Hotkey!;
            var commands = snapshot[key.Code] ??= new string?[16];
            var modifiers = GetModifiers(key.Win, key.Ctrl, key.Shift, key.Alt);
            commands[modifiers] ??= registration.CommandId; // First registration wins.
            hasHotkeys = true;
        }

        if (!HasSameHotkeys(snapshot))
        {
            _hotkeys = snapshot;
            if (Volatile.Read(ref _activeHotkeys) is { } previous)
            {
                Interlocked.CompareExchange(ref _activeHotkeys, snapshot, previous);
            }
        }

        return hasHotkeys;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _stop.Dispose();
    }

    private bool HasSameHotkeys(string?[]?[] snapshot)
    {
        for (var key = 0; key < snapshot.Length; key++)
        {
            if (snapshot[key] is { } commands)
            {
                if (_hotkeys[key] is not { } previous || !commands.AsSpan().SequenceEqual(previous))
                {
                    return false;
                }
            }
            else if (_hotkeys[key] is not null)
            {
                return false;
            }
        }

        return true;
    }

    private void Run(ManualResetEventSlim ready)
    {
        var started = false;
        try
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            var registration = _hook.Install(_hookProc);
            try
            {
                Volatile.Write(ref _activeHotkeys, _hotkeys);
                started = true;
                ready.Set();
                _hook.RunMessageLoop(_stop, () =>
                {
                    IDisposable replacement;
                    try
                    {
                        replacement = _hook.Install(_hookProc);
                    }
                    catch (Exception ex)
                    {
                        // Keep the current hook and retry on the next renewal.
                        _ = Task.Run(() => _reportError(new InvalidOperationException("Failed to renew the global keyboard hook; keeping the current registration", ex)));
                        return;
                    }

                    var previous = registration;
                    registration = replacement;
                    previous.Dispose();
                });
            }
            finally
            {
                Volatile.Write(ref _activeHotkeys, null);
                registration.Dispose();
            }
        }
        catch (Exception ex)
        {
            if (!started)
            {
                _startupError = ex;
            }
            else
            {
                // Report after unhooking; fallback registration belongs to the UI thread.
                var ownerThread = Thread.CurrentThread;
                try
                {
                    _tryEnqueue(() =>
                    {
                        if (_thread == ownerThread)
                        {
                            _hookFailed(ex);
                        }
                    });
                }
                catch (Exception dispatchError)
                {
                    // The hook has already been released before reaching this handler.
                    _reportError(dispatchError);
                }
            }
        }
        finally
        {
            if (!started)
            {
                ready.Set();
            }
        }
    }

    private unsafe LRESULT KeyboardHook(int nCode, WPARAM wParam, LPARAM lParam)
    {
        var matched = false;
        try
        {
            var snapshot = Volatile.Read(ref _activeHotkeys);
            if (snapshot is not null && nCode >= 0 &&
                (wParam.Value == PInvoke.WM_KEYDOWN || wParam.Value == PInvoke.WM_SYSKEYDOWN))
            {
                var key = ((KBDLLHOOKSTRUCT*)lParam.Value)->vkCode;
                if (key < snapshot.Length && snapshot[key] is { } commands)
                {
                    var win = _hook.IsKeyDown(VIRTUAL_KEY.VK_LWIN) || _hook.IsKeyDown(VIRTUAL_KEY.VK_RWIN);
                    var modifiers = GetModifiers(
                        win,
                        _hook.IsKeyDown(VIRTUAL_KEY.VK_CONTROL),
                        _hook.IsKeyDown(VIRTUAL_KEY.VK_SHIFT),
                        _hook.IsKeyDown(VIRTUAL_KEY.VK_MENU));
                    if (commands[modifiers] is { } commandId)
                    {
                        matched = true;
                        var timestamp = _getTimestamp();

                        // Mask modifier-only actions and preserve foreground activation behavior.
                        _hook.SendDummyKeyUp();
                        EnqueueCommand(commandId, timestamp, snapshot);
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

    private void EnqueueCommand(string commandId, long timestamp, string?[]?[] snapshot) =>
        _tryEnqueue(() => ProcessCommand(commandId, timestamp, snapshot));

    private void ProcessCommand(string commandId, long timestamp, string?[]?[] snapshot)
    {
        if (!ReferenceEquals(snapshot, Volatile.Read(ref _activeHotkeys)))
        {
            return;
        }

        try
        {
            _processCommand(commandId, timestamp);
        }
        catch (Exception ex)
        {
            _reportError(ex);
        }
    }

    private static int GetModifiers(bool win, bool ctrl, bool shift, bool alt) =>
        (win ? 8 : 0) | (ctrl ? 4 : 0) | (shift ? 2 : 0) | (alt ? 1 : 0);
}
