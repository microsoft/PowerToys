// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Native
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using Peek.Common.Models;

    public static class NativeMethods
    {
        [Flags]
        public enum AssocF
        {
            None = 0,
            Init_NoRemapCLSID = 0x1,
            Init_ByExeName = 0x2,
            Open_ByExeName = 0x2,
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

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern HResult AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, [In][Out] ref uint pcchOut);

        [DllImport("user32.dll")]
        internal static extern int GetWindowText(Windows.Win32.Foundation.HWND hWnd, StringBuilder lpString, int nMaxCount);
    }
}
