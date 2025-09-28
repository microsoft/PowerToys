// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

internal static partial class NativeMethods
{
    [LibraryImport("shell32.dll", EntryPoint = "SHParseDisplayName", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SHParseDisplayName(
        string pszName,
        nint pbc,
        out nint ppidl,
        uint sfgaoIn,
        nint psfgaoOut);

    [LibraryImport("shell32.dll", EntryPoint = "SHGetNameFromIDList", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SHGetNameFromIDList(
        nint pidl,
        SIGDN sigdnName,
        out nint ppszName);

    [LibraryImport("ole32.dll")]
    internal static partial void CoTaskMemFree(nint pv);

    [LibraryImport("shell32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr LocalFree(IntPtr hMem);

    internal enum SIGDN : uint
    {
        NORMALDISPLAY = 0x00000000,
        DESKTOPABSOLUTEPARSING = 0x80028000,
        DESKTOPABSOLUTEEDITING = 0x8004C000,
        FILESYSPATH = 0x80058000,
        URL = 0x80068000,
        PARENTRELATIVE = 0x80080001,
        PARENTRELATIVEFORADDRESSBAR = 0x8007C001,
        PARENTRELATIVEPARSING = 0x80018001,
        PARENTRELATIVEEDITING = 0x80031001,
        PARENTRELATIVEFORUI = 0x80094001,
    }
}
