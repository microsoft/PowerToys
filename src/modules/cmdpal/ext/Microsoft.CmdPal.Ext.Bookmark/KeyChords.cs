// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal static class KeyChords
{
    internal static KeyChord CopyPath => WellKnownKeyChords.CopyFilePath;

    internal static KeyChord OpenFileLocation => WellKnownKeyChords.OpenFileLocation;

    internal static KeyChord OpenInConsole => WellKnownKeyChords.OpenInConsole;

    internal static KeyChord DeleteBookmark => KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Delete);
}
