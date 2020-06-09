using System;
using System.Runtime.InteropServices;

namespace ExplorerCommandLib.Interop
{
    internal enum SIGDN : uint
    {
        NORMALDISPLAY = 0x00000000,
        PARENTRELATIVEPARSING = 0x80018001,
        DESKTOPABSOLUTEPARSING = 0x80028000,
        PARENTRELATIVEEDITING = 0x80031001,
        DESKTOPABSOLUTEEDITING = 0x8004c000,
        FILESYSPATH = 0x80058000,
        URL = 0x80068000,
        PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        PARENTRELATIVE = 0x80080001,
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object BindToHandler([In, MarshalAs(UnmanagedType.Interface)] object pbc, [In] ref Guid bhid, [In] ref Guid riid);
        void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
        void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
    }
}
