// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Peek.Common.Models
{
    [ComImport]
    [Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem2 : IShellItem
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        new void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public int GetDisplayName([In] int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public void GetAttributes([In] int sfgaoMask, out int psfgaoAttribs);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        new void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);

        [PreserveSig]
        public int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        internal int GetPropertyStoreWithCreateObject(ref GETPROPERTYSTOREFLAGS flags, ref IntPtr punkFactory, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        internal int GetPropertyStoreForKeys(ref PropertyKey keys, uint cKeys, ref GETPROPERTYSTOREFLAGS flags, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        public int GetPropertyDescriptionList(ref PropertyKey key, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        public int Update(ref IntPtr pbc);

        [PreserveSig]
        public int GetProperty(ref PropertyKey key, out PropVariant pPropVar);

        [PreserveSig]
        public int GetCLSID(ref PropertyKey key, out Guid clsid);

        [PreserveSig]
        public int GetFileTime(ref PropertyKey key, out FileTime pft);

        [PreserveSig]
        public int GetInt32(ref PropertyKey key, out int pi);

        [PreserveSig]
        public int GetString(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] string ppsz);

        [PreserveSig]
        public int GetUint32(ref PropertyKey key, out uint pui);

        [PreserveSig]
        public int GetUint64(ref PropertyKey key, out uint pull);

        [PreserveSig]
        public int GetBool(ref PropertyKey key, bool pf);
    }
}
