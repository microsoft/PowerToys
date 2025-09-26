// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Apps;

internal static class KeyChords
{
    internal static KeyChord OpenFileLocation { get; } = WellKnownKeyChords.OpenFileLocation;

    internal static KeyChord CopyFilePath { get; } = WellKnownKeyChords.CopyFilePath;

    internal static KeyChord OpenInConsole { get; } = WellKnownKeyChords.OpenInConsole;

    internal static KeyChord RunAsAdministrator { get; } = WellKnownKeyChords.RunAsAdministrator;

    internal static KeyChord RunAsDifferentUser { get; } = WellKnownKeyChords.RunAsDifferentUser;

    internal static KeyChord TogglePin { get; } = WellKnownKeyChords.TogglePin;
}
