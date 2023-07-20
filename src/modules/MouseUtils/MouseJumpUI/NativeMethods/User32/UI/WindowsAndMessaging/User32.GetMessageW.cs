// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Retrieves a message from the calling thread's message queue.
    /// The function dispatches incoming sent messages until a posted message is available for retrieval.
    /// </summary>
    /// <param name="lpMsg">A pointer to an MSG structure that receives message information from the thread's message queue.</param>
    /// <param name="hWnd">A handle to the window whose messages are to be retrieved. The window must belong to the current thread.</param>
    /// <param name="wMsgFilterMin">The integer value of the lowest message value to be retrieved.</param>
    /// <param name="wMsgFilterMax">The integer value of the highest message value to be retrieved.</param>
    /// <returns>
    /// If the function retrieves a message other than WM_QUIT, the return value is nonzero.
    /// If the function retrieves the WM_QUIT message, the return value is zero.
    /// If there is an error, the return value is -1.
    /// For example, the function fails if hWnd is an invalid window handle or lpMsg is an invalid pointer.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessage
    ///     https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Windows/User32/Interop.GetMessage.cs
    /// </remarks>
    [LibraryImport(Libraries.User32)]
    internal static partial BOOL GetMessageW(
        LPMSG lpMsg,
        HWND hWnd,
        UINT wMsgFilterMin,
        UINT wMsgFilterMax);
}
