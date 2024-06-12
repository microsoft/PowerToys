// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Synthesizes keystrokes, mouse motions, and button clicks.
    /// </summary>
    /// <returns>
    /// The function returns the number of events that it successfully inserted into the keyboard or mouse input stream.
    /// If the function returns zero, the input was already blocked by another thread.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
    /// </remarks>
    [LibraryImport(Libraries.User32, SetLastError = true)]
    internal static partial UINT SendInput(
        UINT cInputs,
        LPINPUT pInputs,
        int cbSize);
}
