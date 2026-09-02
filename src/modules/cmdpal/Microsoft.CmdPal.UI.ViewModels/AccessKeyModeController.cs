// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Coordinates access-key mode, including Alt-tap activation and deferred dismissal.
/// </summary>
public sealed class AccessKeyModeController
{
    private bool _isAltKeyDown;
    private bool _isAltTapCandidate;
    private long _generation;

    /// <summary>
    /// Occurs when <see cref="IsActive"/> changes.
    /// </summary>
    public event EventHandler? IsActiveChanged;

    /// <summary>
    /// Gets a value indicating whether access-key mode is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Processes a key-down and returns a generation when dismissal must wait until after dispatch.
    /// </summary>
    /// <param name="chord">The pressed key and modifiers.</param>
    /// <returns>The generation to validate after dispatch, or <see langword="null"/>.</returns>
    public long? HandleKeyDown(KeyChord chord)
    {
        var key = (VirtualKey)chord.Vkey;
        if (IsAltKey(key))
        {
            _isAltKeyDown = true;
            _isAltTapCandidate =
                !chord.Modifiers.HasFlag(VirtualKeyModifiers.Control) &&
                !chord.Modifiers.HasFlag(VirtualKeyModifiers.Shift) &&
                !chord.Modifiers.HasFlag(VirtualKeyModifiers.Windows);
            return null;
        }

        if (_isAltKeyDown)
        {
            _isAltTapCandidate = false;
        }

        return IsActive && !IsModifierKey(key) ? _generation : null;
    }

    /// <summary>
    /// Processes a key-up, completing an Alt tap when applicable.
    /// </summary>
    /// <param name="key">The released key.</param>
    public void HandleKeyUp(VirtualKey key)
    {
        if (!IsAltKey(key))
        {
            return;
        }

        var shouldToggle = _isAltKeyDown && _isAltTapCandidate;
        ResetAltTapState();

        if (!shouldToggle)
        {
            return;
        }

        if (IsActive)
        {
            Exit();
        }
        else
        {
            _generation++;
            SetIsActive(true);
        }
    }

    /// <summary>
    /// Exits access-key mode and invalidates pending input state.
    /// </summary>
    public void Exit()
    {
        _generation++;
        ResetAltTapState();
        SetIsActive(false);
    }

    /// <summary>
    /// Exits access-key mode if <paramref name="generation"/> is still current.
    /// </summary>
    /// <param name="generation">The generation captured before dispatch.</param>
    public void ExitIfCurrent(long generation)
    {
        if (_generation == generation)
        {
            Exit();
        }
    }

    /// <summary>
    /// Exits access-key mode and invalidates pending work from the previous UI scope.
    /// </summary>
    public void InvalidateScope() => Exit();

    private void SetIsActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        IsActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetAltTapState()
    {
        _isAltKeyDown = false;
        _isAltTapCandidate = false;
    }

    private static bool IsAltKey(VirtualKey key) =>
        key is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu;

    private static bool IsModifierKey(VirtualKey key) =>
        key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
            VirtualKey.LeftWindows or VirtualKey.RightWindows;
}
