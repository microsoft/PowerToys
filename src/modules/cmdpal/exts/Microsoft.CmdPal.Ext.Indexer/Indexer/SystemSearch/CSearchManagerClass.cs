// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39")]
[TypeLibType(2)]
[ClassInterface((short)0)]
[ComConversionLoss]
[ComImport]
public class CSearchManagerClass : ISearchManager, CSearchManager
{
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void GetIndexerVersionStr([MarshalAs(UnmanagedType.LPWStr)] out string ppszVersionString);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern IntPtr GetParameter([MarshalAs(UnmanagedType.LPWStr), In] string pszName);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void SetParameter([MarshalAs(UnmanagedType.LPWStr), In] string pszName, [In] ref object pValue);

    [DispId(1610678276)]
    public virtual extern string ProxyName
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678277)]
    public virtual extern string BypassList
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void SetProxy(
      [In] object sUseProxy,
      [In] int fLocalByPassProxy,
      [In] uint dwPortNumber,
      [MarshalAs(UnmanagedType.LPWStr), In] string pszProxyName,
      [MarshalAs(UnmanagedType.LPWStr), In] string pszByPassList);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    public virtual extern CSearchCatalogManager GetCatalog([MarshalAs(UnmanagedType.LPWStr), In] string pszCatalog);

    [DispId(1610678280)]
    public virtual extern string UserAgent
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: MarshalAs(UnmanagedType.LPWStr)]
        [param: In]
        set;
    }

    [DispId(1610678282)]
    public virtual extern object UseProxy
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678283)]
    public virtual extern int LocalBypass
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678284)]
    public virtual extern uint PortNumber
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }
}
