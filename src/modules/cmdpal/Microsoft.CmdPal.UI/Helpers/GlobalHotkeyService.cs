// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Coordinates low-level and standard registration for global shortcuts.</summary>
/// <remarks>
/// All members and callbacks run on the UI thread. Configuration reloads retry failed standard registrations.
/// A failed low-level installation is retried only after low-level mode is toggled off and on.
/// </remarks>
internal sealed partial class GlobalHotkeyService : IDisposable
{
    private readonly IGlobalKeyboardListener _listener;
    private readonly Action _unregisterHotkeys;
    private readonly Func<TopLevelHotkey, bool> _registerHotkey;
    private readonly Action<string, Exception> _logFailure;
    private readonly Action<bool> _notifyFailure;
    private readonly List<TopLevelHotkey> _failedStandardHotkeys = [];
    private TopLevelHotkey[] _hotkeys = [];
    private bool _configured;
    private bool _useLowLevel;
    private bool _lowLevelFailed;
    private bool _failureReported;
    private bool _summonFailureReported;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="GlobalHotkeyService"/> class.</summary>
    /// <param name="listener">Owned listener, disposed with this service.</param>
    /// <param name="unregisterHotkeys">Removes all standard hotkey registrations.</param>
    /// <param name="registerHotkey">Attempts to register a normalized shortcut and returns whether it succeeded.</param>
    /// <param name="logFailure">Logs hook and cleanup failures independently of notification suppression.</param>
    /// <param name="notifyFailure">Shows a warning; <see langword="true"/> requests an immediate warning because the summon fallback failed.</param>
    public GlobalHotkeyService(
        IGlobalKeyboardListener listener,
        Action unregisterHotkeys,
        Func<TopLevelHotkey, bool> registerHotkey,
        Action<string, Exception> logFailure,
        Action<bool> notifyFailure)
    {
        _listener = listener;
        _unregisterHotkeys = unregisterHotkeys;
        _registerHotkey = registerHotkey;
        _logFailure = logFailure;
        _notifyFailure = notifyFailure;
    }

    /// <summary>Gets whether the current low-level configuration has fallen back to standard registration.</summary>
    public bool IsUsingFallback => _configured && _useLowLevel && _lowLevelFailed;

    /// <summary>Gets whether fallback also failed to register the palette's summon shortcut.</summary>
    public bool IsSummonHotkeyUnavailable => IsUsingFallback && _failedStandardHotkeys.Any(hotkey => string.IsNullOrEmpty(hotkey.CommandId));

    /// <summary>Applies shortcut settings and retries any failed standard registrations.</summary>
    /// <param name="useLowLevel">Whether to prefer the low-level hook over standard registration.</param>
    /// <param name="hotkeys">Requested shortcuts; invalid entries are skipped and the first duplicate wins.</param>
    /// <returns>Whether the configuration changed or a previously failed standard registration succeeded.</returns>
    public bool Configure(bool useLowLevel, IEnumerable<TopLevelHotkey> hotkeys)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var snapshot = Normalize(hotkeys);
        if (_configured && _useLowLevel == useLowLevel && _hotkeys.SequenceEqual(snapshot))
        {
            return RetryStandardHotkeys();
        }

        _configured = false;
        _useLowLevel = useLowLevel;
        _hotkeys = snapshot;
        if (!useLowLevel)
        {
            _lowLevelFailed = false;
        }

        UnregisterHotkeys();
        _failedStandardHotkeys.Clear();
        Exception? error = null;
        if (useLowLevel && !_lowLevelFailed && snapshot.Length > 0)
        {
            _listener.SetHotkeys(snapshot);
            if (_listener.Start(out error))
            {
                _configured = true;
                return true;
            }

            _lowLevelFailed = true;
        }

        RegisterStandardHotkeys();
        _configured = true;
        if (error is not null)
        {
            LogFailure("Global keyboard hook failed; using standard shortcut registration", error);
        }

        NotifyFailureOnce();
        return true;
    }

    /// <summary>Switches an active low-level configuration to standard registration after a fatal hook failure.</summary>
    /// <param name="error">Failure reported after the hook thread has removed its hook.</param>
    public void HandleHookFailure(Exception error)
    {
        if (!_configured || !_useLowLevel || _lowLevelFailed)
        {
            return;
        }

        _lowLevelFailed = true;
        UnregisterHotkeys();
        RegisterStandardHotkeys();
        LogFailure("Global keyboard hook failed; using standard shortcut registration", error);
        NotifyFailureOnce();
    }

    /// <summary>Stops both registration paths and clears pending standard retries.</summary>
    /// <remarks>Cleanup failures are logged. Failure and notification state are retained across restarts.</remarks>
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        StopCore();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopCore();
        _listener.Dispose();
    }

    private void StopCore()
    {
        _configured = false;
        _failedStandardHotkeys.Clear();
        _listener.Stop();
        UnregisterHotkeys();
    }

    private void UnregisterHotkeys()
    {
        try
        {
            _unregisterHotkeys();
        }
        catch (Exception ex)
        {
            LogFailure("Failed to unregister global hotkeys", ex);
        }
    }

    private void LogFailure(string message, Exception error)
    {
        try
        {
            _logFailure(message, error);
        }
        catch (Exception)
        {
            // Logging must not prevent cleanup or fallback notifications.
        }
    }

    private static TopLevelHotkey[] Normalize(IEnumerable<TopLevelHotkey> hotkeys)
    {
        List<TopLevelHotkey> result = [];
        HashSet<HotkeySettings> seen = [];
        foreach (var registration in hotkeys)
        {
            if (registration.Hotkey is not { Code: > 0 and < 256 } key)
            {
                continue;
            }

            var normalizedKey = new HotkeySettings(key.Win, key.Ctrl, key.Alt, key.Shift, key.Code);
            if (seen.Add(normalizedKey))
            {
                result.Add(new(normalizedKey, registration.CommandId));
            }
        }

        return [.. result];
    }

    private void RegisterStandardHotkeys()
    {
        _listener.Stop();
        _failedStandardHotkeys.Clear();
        foreach (var hotkey in _hotkeys)
        {
            if (!_registerHotkey(hotkey))
            {
                _failedStandardHotkeys.Add(hotkey);
            }
        }
    }

    private bool RetryStandardHotkeys()
    {
        var registered = _failedStandardHotkeys.RemoveAll(hotkey => _registerHotkey(hotkey)) > 0;
        NotifyFailureOnce();
        return registered;
    }

    private void NotifyFailureOnce()
    {
        if (!IsUsingFallback || (_failureReported && (!IsSummonHotkeyUnavailable || _summonFailureReported)))
        {
            return;
        }

        _failureReported = true;
        _summonFailureReported |= IsSummonHotkeyUnavailable;
        _notifyFailure(IsSummonHotkeyUnavailable);
    }
}
