// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

internal static partial class NativeMethods
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SHGetFileInfo(IntPtr pidl, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("shell32.dll")]
    internal static extern int SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("ole32.dll")]
    internal static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHLoadIndirectString(string pszSource, System.Text.StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEINFO
    {
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

    [DllImport("comctl32.dll", SetLastError = true)]
    internal static extern int ImageList_GetIcon(IntPtr himl, int i, int flags);

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = false)]
    internal static unsafe partial int AssocQueryStringW(
        AssocF flags,
        AssocStr str,
        string pszAssoc,
        string? pszExtra,
        char* pszOut,
        ref uint pcchOut);

    // SHDefExtractIconW lets us ask for specific sizes (incl. 256)
    // nIconSize: HIWORD = large size, LOWORD = small size
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = false)]
    internal static partial int SHDefExtractIconW(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        out nint phiconLarge,
        out nint phiconSmall,
        int nIconSize);

    [Flags]
    public enum AssocF : uint
    {
        None = 0,
        IsProtocol = 0x00001000,
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
        InfoTip,
        QuickTip,
        TileInfo,
        ContentType,
        DefaultIcon,
        ShellExtension,
        DropTarget,
        DelegateExecute,
        SupportedUriProtocols,
        ProgId,
        AppId,
        AppPublisher,
        AppIconReference, // sometimes present, but DefaultIcon is most common
    }
}
