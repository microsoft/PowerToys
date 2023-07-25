// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Places (posts) a message in the message queue associated with the thread that created the
    /// specified window and returns without waiting for the thread to process the message.
    ///
    /// To post a message in the message queue associated with a thread, use the PostThreadMessage function.
    /// </summary>
    /// <param name="hWnd">A handle to the window whose window procedure is to receive the message.</param>
    /// <param name="Msg">The message to be posted.</param>
    /// <param name="wParam">wParam - Additional message-specific information.</param>
    /// <param name="lParam">lParam - Additional message-specific information.</param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// To get extended error information, call GetLastError.
    /// GetLastError returns ERROR_NOT_ENOUGH_QUOTA when the limit is hit.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew
    ///     https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Windows/User32/Interop.PostMessage.cs
    /// </remarks>
    [LibraryImport(Libraries.User32, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial BOOL PostMessageW(
        HWND hWnd,
        [SuppressMessage("SA1313", "SA1313:ParameterNamesMustBeginWithLowerCaseLetter", Justification = "Parameter name matches Win32 api")]
        MESSAGE_TYPE Msg,
        WPARAM wParam,
        LPARAM lParam);
}
