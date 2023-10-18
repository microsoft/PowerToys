// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Peek.Common.Models;

namespace Peek.Common
{
    public static class NativeMethods
    {
        internal const uint SHGFI_ICON = 0x000000100;
        internal const uint SHGFI_LINKOVERLAY = 0x000008000;
        internal const uint SHGFI_SMALLICON = 0x000000001;
        internal const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        internal const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [DllImport("User32.dll", SetLastError = true)]
        internal static extern int DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
    }
}
