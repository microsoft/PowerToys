// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Owns the low-level global shortcut listener and its hotkey mappings.</summary>
/// <remarks>
/// Call all members, including disposal, from the UI thread.
/// Disposal must stop the hook thread and cancel queued commands before releasing owned resources.
/// Stop and disposal do not throw under this ownership contract.
/// Interrupted waits are retried, then the interrupt is restored on the calling thread.
/// </remarks>
internal interface IGlobalKeyboardListener : IDisposable
{
    /// <summary>Starts the hook thread and waits for installation to finish, or reuses the running listener.</summary>
    /// <param name="error">The startup error on failure, or <see langword="null"/> on success.</param>
    /// <returns>Whether the listener started successfully or was already running.</returns>
    /// <remarks>Installation failures are returned through <paramref name="error"/>; interrupts do not cause startup failure.</remarks>
    bool Start(out Exception? error);

    /// <summary>Stops the hook thread and cancels queued commands, retaining mappings for a later start.</summary>
    /// <remarks>Completes without throwing under the UI-thread ownership contract, including after disposal.</remarks>
    void Stop();

    /// <summary>Replaces the shortcuts handled by the listener.</summary>
    /// <param name="hotkeys">Registrations validated by <see cref="GlobalHotkeyService"/>, with non-null keys and codes from 1 through 255.</param>
    /// <remarks>Unchanged mappings preserve queued commands. Changed mappings cancel them. The first duplicate wins.</remarks>
    void SetHotkeys(IEnumerable<TopLevelHotkey> hotkeys);
}
