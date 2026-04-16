// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ShowDesktop
{
    internal sealed class WindowInfo
    {
        public IntPtr Hwnd { get; }

        public NativeMethods.WINDOWPLACEMENT Placement { get; }

        public WindowInfo(IntPtr hwnd, NativeMethods.WINDOWPLACEMENT placement)
        {
            Hwnd = hwnd;
            Placement = placement;
        }
    }
}
