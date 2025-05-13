// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
[InterfaceType(1)]
[GeneratedComInterface]
public partial interface ISearchManager
{
    void GetIndexerVersionStr([MarshalAs(UnmanagedType.LPWStr)] out string ppszVersionString);

    void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);

    IntPtr GetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName);

    void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.Interface)] ref object pValue);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetProxyName();

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetBypassList();

    void SetProxy(
      [MarshalAs(UnmanagedType.Interface)] object sUseProxy,
      int fLocalByPassProxy,
      uint dwPortNumber,
      [MarshalAs(UnmanagedType.LPWStr)] string pszProxyName,
      [MarshalAs(UnmanagedType.LPWStr)] string pszByPassList);

    ISearchCatalogManager GetCatalog([MarshalAs(UnmanagedType.LPWStr)] string pszCatalog);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetUserAgent();

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetUseProxy();

    int GetLocalBypass();

    uint GetPortNumber();
}
