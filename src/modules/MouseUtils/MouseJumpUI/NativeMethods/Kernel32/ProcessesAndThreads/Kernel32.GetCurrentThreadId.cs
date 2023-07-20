// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class Kernel32
{
    /// <summary>
    /// Retrieves the thread identifier of the calling thread.
    /// </summary>
    /// <returns>
    /// The return value is the thread identifier of the calling thread.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentthreadid
    ///     https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Windows/Kernel32/Interop.GetCurrentThreadId.cs
    /// </remarks>
    [LibraryImport(Libraries.Kernel32)]
    internal static partial DWORD GetCurrentThreadId();
}
