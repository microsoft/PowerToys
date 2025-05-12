// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[ComConversionLoss]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
[InterfaceType(1)]
[GeneratedComInterface]
public partial interface ISearchManager
{

    void GetIndexerVersionStr([MarshalAs(UnmanagedType.LPWStr)] out string ppszVersionString);


    void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);


    IntPtr GetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName);


    void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, [In] ref object pValue);

    [DispId(1610678276)]
    string ProxyName
    {
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678277)]
    string BypassList
    {
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }


    void SetProxy(
      [In] object sUseProxy,
      [In] int fLocalByPassProxy,
      [In] uint dwPortNumber,
      [MarshalAs(UnmanagedType.LPWStr), In] string pszProxyName,
      [MarshalAs(UnmanagedType.LPWStr), In] string pszByPassList);


    [return: MarshalAs(UnmanagedType.Interface)]
    CSearchCatalogManager GetCatalog([MarshalAs(UnmanagedType.LPWStr), In] string pszCatalog);

    [DispId(1610678280)]
    string UserAgent
    {
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    
        [param: MarshalAs(UnmanagedType.LPWStr)]

        set;
    }

    [DispId(1610678282)]
    object UseProxy
    {
    
        get;
    }

    [DispId(1610678283)]
    int LocalBypass
    {
    
        get;
    }

    [DispId(1610678284)]
    uint PortNumber
    {
    
        get;
    }
}
