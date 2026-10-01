// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace PowerToysExtension;

internal static class KeyChords
{
    internal static readonly KeyChord RunAsAdministrator = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.Enter);
}
