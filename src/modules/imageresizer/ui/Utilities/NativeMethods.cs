// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1636:FileHeaderCopyrightTextMustMatch", Justification = "File created under PowerToys.")]

namespace ImageResizer.Utilities;

internal partial class NativeMethods
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessDPIAware();

    /// <summary>
    /// Shell File Operations structure. Used for file deletion.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public char* pFrom;
        public char* pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;   // Win32 BOOL is 4 bytes
        public IntPtr hNameMappings;
        public char* lpszProgressTitle;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial int SHFileOperationW(SHFILEOPSTRUCT* fileOp);

    /// <summary>
    /// File delete operation.
    /// </summary>
    internal const uint FO_DELETE = 0x0003;

    /// <summary>
    /// Do not display a progress dialog box during shell file operations.
    /// </summary>
    internal const int FOF_SILENT = 0x0004;

    /// <summary>
    /// Send to Recycle Bin flag.
    /// </summary>
    internal const ushort FOF_ALLOWUNDO = 0x0040;

    /// <summary>
    /// Do not request user confirmation for file deletes.
    /// </summary>
    internal const ushort FOF_NO_CONFIRMATION = 0x0010;

    /// <summary>
    /// Do not display any error UI to the user.
    /// </summary>
    internal const ushort FOF_NOERRORUI = 0x400;

    /// <summary>
    /// Warn if a file cannot be recycled and would instead be permanently deleted. (Partially
    /// overrides FOF_NO_CONFIRMATION.) This can be tested by attempting to delete a file on a
    /// FAT volume, e.g. a USB key.
    /// </summary>
    /// <remarks>Declared in shellapi.h./remarks>
    internal const ushort FOF_WANTNUKEWARNING = 0x4000;

    /// <summary>
    /// The user cancelled the delete operation. Not classified as an error for our purposes.
    /// </summary>
    internal const int ERROR_CANCELLED = 1223;

    /// <summary>
    /// Success return code.
    /// </summary>
    internal const int S_OK = 0;

    /// <summary>
    /// "Unspecified error" - returned by SHQueryRecycleBinW when no Recycle Bin exists.
    /// </summary>
    internal const int E_FAIL = unchecked((int)0x80004005);

    /// <summary>
    /// Shell Change Notify. Used to inform shell listeners after we've completed a file
    /// operation like Delete or Move.
    /// </summary>
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>
    /// File System Notification Flag, indicating that the operation was a file deletion.
    /// </summary>
    /// <remarks>See ShlObj_core.h for constant definitions.</remarks>
    internal const uint SHCNE_DELETE = 0x00000004;

    /// <summary>
    /// Indicates that SHChangeNotify's dwItem1 and (optionally) dwItem2 parameters will
    /// contain string paths.
    /// </summary>
    internal const uint SHCNF_PATH = 0x0001;

    /// <summary>
    /// Retrieves the size of the Recycle Bin and the number of items contained within for a
    /// specified drive.
    /// </summary>
    /// <param name="pszRootPath">The root directory of the drive to query, e.g. <c>C:\</c>.</param>
    /// <param name="pSHQueryRBInfo">The <see cref="SHQUERYRBINFO"/> structure which receives
    /// the requested information.</param>
    /// <returns>An HRESULT, <c>0</c> on success, non-zero on failure.</returns>
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial int SHQueryRecycleBinW(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SHQUERYRBINFO
    {
        public uint cbSize;
        public ulong i64Size;
        public ulong i64NumItems;
    }
}
