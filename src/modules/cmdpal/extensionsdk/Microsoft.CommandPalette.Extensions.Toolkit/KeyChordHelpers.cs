// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using Windows.System;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class KeyChordHelpers
{
    public static KeyChord FromModifiers(bool ctrl, bool alt, bool shift, bool win, int vkey, int scanCode)
    {
        var modifiers = (ctrl ? VirtualKeyModifiers.Control : VirtualKeyModifiers.None)
            | (alt ? VirtualKeyModifiers.Menu : VirtualKeyModifiers.None)
            | (shift ? VirtualKeyModifiers.Shift : VirtualKeyModifiers.None)
            | (win ? VirtualKeyModifiers.Windows : VirtualKeyModifiers.None)
            ;
        return new(modifiers, vkey, scanCode);
    }
}
