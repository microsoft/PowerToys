// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

internal static class NativeMethods
{
    [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetOpenFileName([In, Out] OpenFileName openFileName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHBrowseForFolderW(ref ShellGetFolder.BrowseInformation browseInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHGetPathFromIDListW(IntPtr pidl, IntPtr path);
}
