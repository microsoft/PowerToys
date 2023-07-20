// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Posts a message to the message queue of the specified thread. It returns without waiting for the thread to process the message.
    /// </summary>
    /// <param name="idThread">The identifier of the thread to which the message is to be posted.</param>
    /// <param name="Msg">The type of message to be posted.</param>
    /// <param name="wParam">wParam - Additional message-specific information.</param>
    /// <param name="lParam">lParam - Additional message-specific information.</param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// To get extended error information, call GetLastError.GetLastError returns ERROR_INVALID_THREAD_ID
    /// if idThread is not a valid thread identifier, or if the thread specified by idThread does not have
    /// a message queue.GetLastError returns ERROR_NOT_ENOUGH_QUOTA when the message limit is hit.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew
    /// </remarks>
    [LibraryImport(Libraries.User32, StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial BOOL PostThreadMessageW(
        DWORD idThread,
        MESSAGE_TYPE Msg,
        WPARAM wParam,
        LPARAM lParam);
}
