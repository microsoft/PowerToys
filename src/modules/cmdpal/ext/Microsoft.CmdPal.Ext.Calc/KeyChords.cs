// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Calc;

internal static class KeyChords
{
    internal static KeyChord CopyResultToSearchBox { get; } = new(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, (int)VirtualKey.Enter, 0);
}
