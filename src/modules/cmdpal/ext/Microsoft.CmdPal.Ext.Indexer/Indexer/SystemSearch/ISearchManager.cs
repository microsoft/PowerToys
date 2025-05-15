// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[GeneratedComInterface]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Please do not change the function name")]
public partial interface ISearchManager
{
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    ISearchCatalogManager GetCatalog([MarshalAs(UnmanagedType.LPWStr)] string pszCatalog);

    void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);

    [return: MarshalAs(UnmanagedType.BStr)]
    string GetIndexerVersionStr();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_UserAgent();

    void put_UserAgent([MarshalAs(UnmanagedType.BStr)] string pszUserAgent);

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_ProxyName();

    uint get_PortNumber();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_BypassList();

    [return: MarshalAs(UnmanagedType.Bool)]
    bool get_UseProxy();

    [return: MarshalAs(UnmanagedType.Bool)]
    bool get_LocalBypass();

    void SetProxy(
        [MarshalAs(UnmanagedType.BStr)] string pszProxyName,
        [MarshalAs(UnmanagedType.Bool)] bool fLocalBypass,
        [MarshalAs(UnmanagedType.BStr)] string pszBypassList,
        uint dwPortNumber);

    void GetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.Struct)] out object pValue);

    void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.Struct)] ref object pValue);
}
