// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Indexer;

internal static class KeyChords
{
    internal static KeyChord OpenFileLocation { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.E);

    internal static KeyChord CopyFilePath { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.C);

    internal static KeyChord OpenInConsole { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.R);
}
