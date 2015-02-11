using System;
using System.Runtime.InteropServices;

namespace Wox.ShellContext
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    public interface IShellFolder
    {
        void ParseDisplayName(
            IntPtr hwnd,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten,
            out IntPtr ppidl,
            ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hWnd, SHCONTF flags, out IntPtr enumIDList);

        void BindToObject(
            IntPtr pidl,
            IntPtr pbc,
            [In()] ref Guid riid,
            out IShellFolder ppv);

        void BindToStorage(
            IntPtr pidl,
            IntPtr pbc,
            [In()] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [PreserveSig()]
        uint CompareIDs(
            int lParam,
            IntPtr pidl1,
            IntPtr pidl2);

        void CreateViewObject(
            IntPtr hwndOwner,
            [In()] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv);

        void GetAttributesOf(
            uint cidl,
            [In(), MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
           ref SFGAO rgfInOut);

        //[PreserveSig]
        //Int32 GetUIObjectOf(
        //    IntPtr hwndOwner,
        //    uint cidl,
        //    [MarshalAs(UnmanagedType.LPArray)]
        //    IntPtr[] apidl,
        //    Guid riid,
        //    IntPtr rgfReserved,
        //    out IntPtr ppv);
        IntPtr GetUIObjectOf(
            IntPtr hwndOwner,
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            [In()] ref Guid riid,
            out IntPtr rgfReserved);

        void GetDisplayNameOf(
            IntPtr pidl,
            SHGNO uFlags,
            IntPtr lpName);

        IntPtr SetNameOf(
            IntPtr hwnd,
            IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
           SHGNO uFlags);
    }
}
