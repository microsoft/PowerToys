// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.System;

namespace ScreenTranslator.Keyboard;

public sealed class GlobalKeyboardHookEventArgs : EventArgs
{
    public VirtualKey Key { get; }

    public bool IsKeyDown { get; }

    public bool Handled { get; set; }

    public GlobalKeyboardHookEventArgs(VirtualKey key, bool isKeyDown)
    {
        Key = key;
        IsKeyDown = isKeyDown;
    }
}
