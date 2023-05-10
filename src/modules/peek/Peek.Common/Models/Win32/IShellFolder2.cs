// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Shell;

namespace Peek.Common.Models
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("93F2F68C-1D1B-11D3-A30E-00C04F79ABD1")]
    public interface IShellFolder2
    {
        [PreserveSig]
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref int pchEaten, out IntPtr ppidl, ref int pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, _SHCONTF grfFlags, out IntPtr enumIDList);

        [PreserveSig]
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        [PreserveSig]
        int CreateViewObject(IntPtr hwndOwner, Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetAttributesOf(int cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref IntPtr rgfInOut);

        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, int cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        [PreserveSig]
        int GetDisplayNameOf(IntPtr pidl, SHGDNF uFlags, out Strret lpName);

        [PreserveSig]
        int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, int uFlags, out IntPtr ppidlOut);

        [PreserveSig]
        int EnumSearches(out IntPtr ppenum);

        [PreserveSig]
        int GetDefaultColumn(int dwReserved, ref IntPtr pSort, out IntPtr pDisplay);

        [PreserveSig]
        int GetDefaultColumnState(int iColumn, out IntPtr pcsFlags);

        [PreserveSig]
        int GetDefaultSearchGUID(out IntPtr pguid);

        [PreserveSig]
        int GetDetailsEx(IntPtr pidl, IntPtr pscid, out IntPtr pv);

        [PreserveSig]
        int GetDetailsOf(IntPtr pidl, int iColumn, ref SHELLDETAILS psd);

        [PreserveSig]
        int MapColumnToSCID(int iColumn, IntPtr pscid);
    }
}
