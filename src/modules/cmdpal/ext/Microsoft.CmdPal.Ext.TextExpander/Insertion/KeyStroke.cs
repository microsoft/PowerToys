// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>
/// One key event: a virtual key, or, when <see cref="Character"/> is set, a character sent as Unicode input, which
/// doesn't depend on the keyboard layout.
/// </summary>
internal readonly record struct KeyStroke(ushort VirtualKey, char Character, bool Up)
{
    public static KeyStroke KeyDown(ushort virtualKey) => new(virtualKey, '\0', false);

    public static KeyStroke KeyUp(ushort virtualKey) => new(virtualKey, '\0', true);

    public static KeyStroke CharacterDown(char character) => new(0, character, false);

    public static KeyStroke CharacterUp(char character) => new(0, character, true);
}
