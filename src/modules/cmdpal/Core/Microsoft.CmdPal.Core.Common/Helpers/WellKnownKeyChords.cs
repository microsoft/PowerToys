// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

using Windows.System;

namespace Microsoft.CmdPal.Core.Common.Helpers;

/// <summary>
/// Well-known key chords used in the Command Palette and extensions.
/// </summary>
/// <remarks>
/// Assigned key chords should not conflict with system or application shortcuts.
/// However, the key chords in this class are not guaranteed to be unique and may conflict
/// with each other, especially when commands appear together in the same menu.
/// </remarks>
public static class WellKnownKeyChords
{
    /// <summary>
    /// Gets the well-known key chord for opening the file location. Shortcut: Ctrl+Shift+E.
    /// </summary>
    public static KeyChord OpenFileLocation { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.E);

    /// <summary>
    /// Gets the well-known key chord for copying the file path. Shortcut: Ctrl+Shift+C.
    /// </summary>
    public static KeyChord CopyFilePath { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.C);

    /// <summary>
    /// Gets the well-known key chord for opening the current location in a console. Shortcut: Ctrl+Shift+R.
    /// </summary>
    public static KeyChord OpenInConsole { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.R);

    /// <summary>
    /// Gets the well-known key chord for running the selected item as administrator. Shortcut: Ctrl+Shift+Enter.
    /// </summary>
    public static KeyChord RunAsAdministrator { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.Enter);

    /// <summary>
    /// Gets the well-known key chord for running the selected item as a different user. Shortcut: Ctrl+Shift+U.
    /// </summary>
    public static KeyChord RunAsDifferentUser { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.U);

    /// <summary>
    /// Gets the well-known key chord for toggling the pin state. Shortcut: Ctrl+P.
    /// </summary>
    public static KeyChord TogglePin { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.P);
}
