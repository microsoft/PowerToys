// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Registers a window class for subsequent use in calls to the CreateWindow or CreateWindowEx function.
    /// </summary>
    /// <param name="unnamedParam1">
    /// A pointer to a WNDCLASSEX structure.
    /// You must fill the structure with the appropriate class attributes before passing it to the function.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is a class atom that uniquely identifies the class being registered.
    /// This atom can only be used by the CreateWindow, CreateWindowEx, GetClassInfo, GetClassInfoEx, FindWindow,
    /// FindWindowEx, and UnregisterClass functions and the IActiveIMMap::FilterClientWindows method.
    /// If the function fails, the return value is zero.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexw
    /// </remarks>
    [DllImport(Libraries.User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ATOM RegisterClassExW(
        WNDCLASSEXW unnamedParam1);
}
