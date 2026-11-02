// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Applies shortcut changes to native registrations or a low-level keyboard listener.
/// </summary>
/// <remarks>
/// Call members on the window's UI thread; callbacks execute synchronously and propagate exceptions.
/// Native IDs are never reassigned to a different binding during this instance's lifetime.
/// Hook installation and window lifetime remain owned by the caller.
/// </remarks>
// TODO: This is a setup for a future refactor on top of PR 50982
internal sealed class HotkeyRegistrar
{
    private readonly Func<int, HotkeySettings, (bool Success, int ErrorCode)> _registerNative;
    private readonly Action<int> _unregisterNative;
    private readonly Action<HotkeySettings, string> _registerHook;
    private readonly Action<HotkeySettings> _unregisterHook;
    private readonly Dictionary<int, TopLevelHotkey> _hotkeys = [];
    private readonly Dictionary<TopLevelHotkey, int> _hotkeyIds = [];
    private readonly HashSet<TopLevelHotkey> _failedRegistrations = [];
    private ImmutableDictionary<TopLevelHotkey, string> _lastConflicts = ImmutableDictionary<TopLevelHotkey, string>.Empty;
    private HotkeyRegistration? _lastRegistration;
    private int _nextHotkeyId;

    /// <summary>Initializes a new instance of the <see cref="HotkeyRegistrar"/> class.</summary>
    /// <param name="registerNative">Registers a native shortcut and returns its success and Win32 error code.</param>
    /// <param name="unregisterNative">Removes a native registration by ID.</param>
    /// <param name="registerHook">Adds a normalized key-to-command mapping to the listener.</param>
    /// <param name="unregisterHook">Removes a normalized key combination from the listener.</param>
    public HotkeyRegistrar(
        Func<int, HotkeySettings, (bool Success, int ErrorCode)> registerNative,
        Action<int> unregisterNative,
        Action<HotkeySettings, string> registerHook,
        Action<HotkeySettings> unregisterHook)
    {
        _registerNative = registerNative;
        _unregisterNative = unregisterNative;
        _registerHook = registerHook;
        _unregisterHook = unregisterHook;
    }

    /// <summary>Updates changed bindings, retries failed native registrations, and logs new conflicts or failures.</summary>
    /// <param name="registration">Desired shortcut mappings and registration mode.</param>
    /// <remarks>Removals precede additions. Unchanged successful bindings are retained unless the registration mode changes.</remarks>
    public void Update(HotkeyRegistration registration)
    {
        foreach (var conflict in registration.Conflicts)
        {
            if (!_lastConflicts.TryGetValue(conflict.Key, out var previousOwner) || previousOwner != conflict.Value)
            {
                Logger.LogWarning($"Skipping duplicate hotkey {conflict.Key.Hotkey} for '{conflict.Key.CommandId}'; already assigned to '{conflict.Value}'.");
            }
        }

        _lastConflicts = registration.Conflicts;
        _failedRegistrations.RemoveWhere(binding => registration.UseLowLevelGlobalHotkey
            || !registration.Hotkeys.TryGetValue(binding.Hotkey!, out var commandId)
            || commandId != binding.CommandId);

        if (registration.Matches(_lastRegistration))
        {
            return;
        }

        var (removed, added) = registration.GetChanges(_lastRegistration);
        if (_lastRegistration?.UseLowLevelGlobalHotkey == true)
        {
            foreach (var key in removed.Keys)
            {
                _unregisterHook(key);
            }
        }
        else
        {
            foreach (var hotkey in _hotkeys.Where(hotkey => removed.ContainsKey(hotkey.Value.Hotkey!)).ToArray())
            {
                _unregisterNative(hotkey.Key);
                _hotkeys.Remove(hotkey.Key);
            }
        }

        List<HotkeySettings> failed = [];
        foreach (var hotkey in added)
        {
            if (registration.UseLowLevelGlobalHotkey)
            {
                _registerHook(hotkey.Key, hotkey.Value);
            }
            else if (!RegisterNative(hotkey.Key, hotkey.Value))
            {
                failed.Add(hotkey.Key);
            }
        }

        // Retry failed keys on a later refresh without releasing successful registrations.
        _lastRegistration = registration.WithoutHotkeys(failed);
    }

    /// <summary>Resolves a WM_HOTKEY message against successful native registrations.</summary>
    /// <param name="id">Native registration ID from WPARAM.</param>
    /// <param name="data">LPARAM containing the modifier flags and virtual-key code.</param>
    /// <returns>The command ID, or <see langword="null"/> in hook mode or when the message does not match.</returns>
    public string? GetCommand(int id, nint data)
    {
        return _lastRegistration?.UseLowLevelGlobalHotkey == false
            ? HotkeyWindowMessageHelper.GetCommand(_hotkeys, id, data)
            : null;
    }

    private bool RegisterNative(HotkeySettings key, string commandId)
    {
        var binding = new TopLevelHotkey(key, commandId);
        if (!_hotkeyIds.TryGetValue(binding, out var hotkeyId))
        {
            // Never reuse an ID for a different binding: WM_HOTKEY may already be queued.
            if (_nextHotkeyId > 0xBFFF)
            {
                if (_failedRegistrations.Add(binding))
                {
                    Logger.LogWarning("Cannot register another distinct hotkey binding: the window's hotkey IDs are exhausted.");
                }

                return false;
            }

            hotkeyId = _nextHotkeyId++;
            _hotkeyIds.Add(binding, hotkeyId);
        }

        var (success, errorCode) = _registerNative(hotkeyId, key);
        if (!success)
        {
            if (_failedRegistrations.Add(binding))
            {
                Logger.LogWarning($"Failed to register hotkey {key} for '{commandId}'. Error: {errorCode}.");
            }

            return false;
        }

        _failedRegistrations.Remove(binding);
        _hotkeys.Add(hotkeyId, binding);
        return true;
    }
}
