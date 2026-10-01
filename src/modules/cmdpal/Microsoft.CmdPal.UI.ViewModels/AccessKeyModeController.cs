// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Coordinates access-key mode, including Alt-tap activation and deferred dismissal.
/// </summary>
public sealed partial class AccessKeyModeController : IDisposable
{
    private IDisposable? _inputHandler;
    private bool _isAltTapCandidate;
    private long _generation;

    /// <summary>
    /// Occurs when <see cref="IsActive"/> changes.
    /// </summary>
    public event EventHandler? IsActiveChanged;

    /// <summary>
    /// Occurs when native access-key mode must also exit, even if CmdPal's mode is already inactive.
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Gets a value indicating whether access-key mode is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets a value indicating whether native display mode must stay disabled for the handled input sequence.
    /// </summary>
    public bool IsNativeDisplayModeSuppressed { get; private set; }

    /// <summary>
    /// Creates and owns the input handler, disposing any previously attached handler.
    /// </summary>
    /// <param name="createInputHandler">Creates the UI input handler for this controller.</param>
    public void AttachInput(Func<AccessKeyModeController, IDisposable> createInputHandler)
    {
        ArgumentNullException.ThrowIfNull(createInputHandler);
        Dispose();
        _inputHandler = createInputHandler(this);
    }

    /// <summary>
    /// Processes a key-down and returns a generation when dismissal must wait until after dispatch.
    /// </summary>
    /// <param name="chord">The pressed key and modifiers.</param>
    /// <returns>The generation to validate after dispatch, or <see langword="null"/>.</returns>
    public long? HandleKeyDown(KeyChord chord)
    {
        IsNativeDisplayModeSuppressed = false;
        var key = (VirtualKey)chord.Vkey;
        if (IsAltKey(key))
        {
            _isAltTapCandidate =
                !chord.Modifiers.HasFlag(VirtualKeyModifiers.Control) &&
                !chord.Modifiers.HasFlag(VirtualKeyModifiers.Shift) &&
                !chord.Modifiers.HasFlag(VirtualKeyModifiers.Windows);
            return null;
        }

        _isAltTapCandidate = false;
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

        var shouldToggle = _isAltTapCandidate;
        _isAltTapCandidate = false;

        if (!shouldToggle)
        {
            return;
        }

        // WinUI processes this same Alt release after the keyboard hook and toggles its own mode.
        _generation++;
        SetIsActive(!IsActive);
    }

    /// <summary>
    /// Suppresses native display-mode activation for a handled Alt shortcut.
    /// </summary>
    public void SuppressNativeDisplayMode() => IsNativeDisplayModeSuppressed = true;

    /// <summary>
    /// Synchronizes CmdPal's cues with native display mode without requesting another native transition.
    /// </summary>
    /// <param name="isEnabled">Whether native display mode is enabled for CmdPal.</param>
    public void HandleNativeDisplayModeChanged(bool isEnabled)
    {
        _generation++;
        _isAltTapCandidate = false;
        SetIsActive(isEnabled);
    }

    /// <summary>
    /// Exits managed and native access-key mode and cancels pending input.
    /// </summary>
    public void Exit()
    {
        IsNativeDisplayModeSuppressed = false;
        InvalidateScope();
    }

    /// <summary>
    /// Exits access-key mode if the generation is still current and native display mode has ended.
    /// </summary>
    /// <param name="generation">The generation captured before dispatch.</param>
    /// <param name="isNativeDisplayModeEnabled">Whether WinUI is still accepting access keys after dispatch.</param>
    public void ExitIfCurrent(long generation, bool isNativeDisplayModeEnabled)
    {
        if (_generation == generation && !isNativeDisplayModeEnabled)
        {
            InvalidateScope();
        }
    }

    /// <summary>
    /// Exits managed and native access-key mode, preserving suppression for the current Alt sequence.
    /// </summary>
    public void InvalidateScope()
    {
        _generation++;
        _isAltTapCandidate = false;
        SetIsActive(false);
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes the owned input handler.
    /// </summary>
    public void Dispose()
    {
        _inputHandler?.Dispose();
        _inputHandler = null;
    }

    private void SetIsActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        IsActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsAltKey(VirtualKey key) =>
        key is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu;

    private static bool IsModifierKey(VirtualKey key) =>
        key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
            VirtualKey.LeftWindows or VirtualKey.RightWindows;
}
