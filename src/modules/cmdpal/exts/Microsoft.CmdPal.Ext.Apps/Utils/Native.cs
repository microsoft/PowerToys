// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "We want plugins to share this NativeMethods class, instead of each one creating its own.")]
public sealed class Native
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, nint ppvReserved);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, nint pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern HRESULT SHCreateStreamOnFileEx(string fileName, STGM grfMode, uint attributes, bool create, System.Runtime.InteropServices.ComTypes.IStream reserved, out System.Runtime.InteropServices.ComTypes.IStream stream);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern HRESULT SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, nint ppvReserved);

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

        /// <summary>
        /// The operation was canceled by the user. (Error source 7 means Win32.)
        /// </summary>
        /// <SeeAlso href="https://learn.microsoft.com/windows/win32/debug/system-error-codes--1000-1299-"/>
        /// <SeeAlso href="https://en.wikipedia.org/wiki/HRESULT"/>
        E_CANCELLED = 0x800704C7,
    }

    public static class ShellItemTypeConstants
    {
        /// <summary>
        /// Guid for type IShellItem.
        /// </summary>
        public static readonly Guid ShellItemGuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        /// <summary>
        /// Guid for type IShellItem2.
        /// </summary>
        public static readonly Guid ShellItem2Guid = new("7E9FB0D3-919F-4307-AB2E-9B1860310C93");
    }

    /// <summary>
    /// The following are ShellItem DisplayName types.
    /// </summary>
    [Flags]
    public enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        PARENTRELATIVEPARSING = 0x80018001,
        PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
        DESKTOPABSOLUTEPARSING = 0x80028000,
        PARENTRELATIVEEDITING = 0x80031001,
        DESKTOPABSOLUTEEDITING = 0x8004c000,
        FILESYSPATH = 0x80058000,
        URL = 0x80068000,
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    public interface IShellItem
    {
        void BindToHandler(
            nint pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out nint ppv);

        void GetParent(out IShellItem ppsi);

        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    /// <summary>
    /// <see href="https://learn.microsoft.com/windows/win32/stg/stgm-constants">see all STGM values</see>
    /// </summary>
    [Flags]
    public enum STGM : long
    {
        READ = 0x00000000L,
        WRITE = 0x00000001L,
        READWRITE = 0x00000002L,
        CREATE = 0x00001000L,
    }
}
