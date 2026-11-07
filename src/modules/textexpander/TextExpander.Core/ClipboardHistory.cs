// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Whether clipboard history could be read for a snippet that picks from it.</summary>
public enum ClipboardHistoryState
{
    /// <summary>Read: its text copies are in <see cref="ClipboardHistory.Copies"/>, which may be none.</summary>
    Read,

    /// <summary>Clipboard history is off, by the user or by policy.</summary>
    Off,

    /// <summary>It couldn't be read, or not in time.</summary>
    Unreadable,
}

/// <summary>
/// The text copies in clipboard history, newest first, each different one once, read for one expansion of a snippet that
/// picks from it, as {{clipboard:pick}} does. Never kept or logged.
/// </summary>
/// <param name="State">Whether it could be read.</param>
/// <param name="Copies">The text copies, newest first; none unless <paramref name="State"/> is <see cref="ClipboardHistoryState.Read"/>.</param>
public sealed record ClipboardHistory(ClipboardHistoryState State, IReadOnlyList<string> Copies)
{
    /// <summary>Gets clipboard history that's off.</summary>
    public static ClipboardHistory TurnedOff { get; } = new(ClipboardHistoryState.Off, Array.Empty<string>());

    /// <summary>Gets clipboard history that couldn't be read.</summary>
    public static ClipboardHistory NotRead { get; } = new(ClipboardHistoryState.Unreadable, Array.Empty<string>());
}
