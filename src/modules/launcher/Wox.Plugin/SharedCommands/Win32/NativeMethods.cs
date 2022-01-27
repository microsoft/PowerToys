// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

namespace Wox.Plugin.Common.Win32
{
    [SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "We want plugins to share this NativeMethods class, instead of each one creating its own.")]
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(uint threadId, ShellCommand.EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hwnd);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern HRESULT SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);
    }

    [SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "These values are used by win32 api")]
    [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "These are the names win32 api uses.")]
    public enum HRESULT : uint
    {
        /// <summary>
        /// Operation successful.
        /// </summary>
        S_OK = 0x00000000,

        /// <summary>
        /// Operation successful. (negative condition/no operation)
        /// </summary>
        S_FALSE = 0x00000001,

        /// <summary>
        /// Not implemented.
        /// </summary>
        E_NOTIMPL = 0x80004001,

        /// <summary>
        /// No such interface supported.
        /// </summary>
        E_NOINTERFACE = 0x80004002,

        /// <summary>
        /// Pointer that is not valid.
        /// </summary>
        E_POINTER = 0x80004003,

        /// <summary>
        /// Operation aborted.
        /// </summary>
        E_ABORT = 0x80004004,

        /// <summary>
        /// Unspecified failure.
        /// </summary>
        E_FAIL = 0x80004005,

        /// <summary>
        /// Unexpected failure.
        /// </summary>
        E_UNEXPECTED = 0x8000FFFF,

        /// <summary>
        /// General access denied error.
        /// </summary>
        E_ACCESSDENIED = 0x80070005,

        /// <summary>
        /// Handle that is not valid.
        /// </summary>
        E_HANDLE = 0x80070006,

        /// <summary>
        /// Failed to allocate necessary memory.
        /// </summary>
        E_OUTOFMEMORY = 0x8007000E,

        /// <summary>
        /// One or more arguments are not valid.
        /// </summary>
        E_INVALIDARG = 0x80070057,
    }
}
