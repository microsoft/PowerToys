// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Indexer;

internal static class KeyChords
{
    internal static KeyChord OpenFileLocation { get; } = WellKnownKeyChords.OpenFileLocation;

    internal static KeyChord CopyFilePath { get; } = WellKnownKeyChords.CopyFilePath;

    internal static KeyChord OpenInConsole { get; } = WellKnownKeyChords.OpenInConsole;
}
