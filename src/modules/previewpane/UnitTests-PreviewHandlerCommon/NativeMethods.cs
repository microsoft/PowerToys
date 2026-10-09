// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PreviewHandlerCommonUnitTests
{
    public static class NativeMethods
    {
        // Gets the ancestor window: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getancestor
        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    }
}
