// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

internal static class KeyChords
{
    internal static KeyChord DeleteEntry { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Delete);

    internal static KeyChord OpenUrl { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.O);
}
