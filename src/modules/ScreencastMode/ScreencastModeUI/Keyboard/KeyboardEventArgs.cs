// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.System;

namespace ScreencastModeUI.Keyboard
{
    /// <summary>
    /// Event arguments for keyboard events.
    /// </summary>
    internal sealed class KeyboardEventArgs : EventArgs
    {
        public VirtualKey Key { get; }

        public bool IsKeyDown { get; }

        public KeyboardEventArgs(VirtualKey key, bool isKeyDown)
        {
            Key = key;
            IsKeyDown = isKeyDown;
        }
    }
}
