// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Peek.Common.Models;

namespace Peek.UI.Native
{
    public static class NativeMethods
    {
        [Flags]
        public enum AssocF
        {
            None = 0,
            Init_NoRemapCLSID = 0x1,
            Init_ByExeName = 0x2,
            Open_ByExeName = 0x3,
            Init_DefaultToStar = 0x4,
            Init_DefaultToFolder = 0x8,
            NoUserSettings = 0x10,
            NoTruncate = 0x20,
            Verify = 0x40,
            RemapRunDll = 0x80,
            NoFixUps = 0x100,
            IgnoreBaseClass = 0x200,
        }

        public enum AssocStr
        {
            Command = 1,
            Executable,
            FriendlyDocName,
            FriendlyAppName,
            NoOpen,
            ShellNewValue,
            DDECommand,
            DDEIfExec,
            DDEApplication,
            DDETopic,
        }

        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern HResult AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, [In][Out] ref uint pcchOut);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(Windows.Win32.Foundation.HWND hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int nMaxCount);

        /// <summary>
        /// Shell File Operations structure. Used for file deletion.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public int wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        internal static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

        /// <summary>
        /// File delete operation.
        /// </summary>
        internal const int FO_DELETE = 0x0003;

        /// <summary>
        /// Send to Recycle Bin flag.
        /// </summary>
        internal const int FOF_ALLOWUNDO = 0x0040;

        /// <summary>
        /// Do not request user confirmation for file delete flag.
        /// </summary>
        internal const int FOF_NO_CONFIRMATION = 0x0010;

        /// <summary>
        /// Common error codes when calling SHFileOperation to delete a file.
        /// </summary>
        /// <remarks>See winerror.h for full list.</remarks>
        public static readonly Dictionary<int, string> DeleteFileErrors = new()
        {
            { 2, "The system cannot find the file specified." },
            { 3, "The system cannot find the path specified." },
            { 5, "Access is denied." },
            { 19, "The media is write protected." },
            { 32, "The process cannot access the file because it is being used by another process." },
            { 33, "The process cannot access the file because another process has locked a portion of the file." },
        };
    }
}
