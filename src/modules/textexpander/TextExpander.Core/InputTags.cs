// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Tags for the <c>dwExtraInfo</c> of key events Text Expander's parts send, kept here so they can't drift apart.</summary>
internal static class InputTags
{
    /// <summary>
    /// Tags what Command Palette's Text Expander extension pastes or types. It has Keyboard Manager's injected bit
    /// (<c>KEYBOARDMANAGER_INJECTED_FLAG</c>, 0x1), so a remap such as Caps Lock and Ctrl swapped leaves the extension's
    /// Ctrl+V alone, in a value of its own, which the engine's hook ignores, so what the extension types never forms a
    /// trigger. Not 0x111, which Keyboard Manager swallows, nor the runner's 0x110, which lacks the bit.
    /// </summary>
    public const int CommandPalette = 0x5445_0001;
}
