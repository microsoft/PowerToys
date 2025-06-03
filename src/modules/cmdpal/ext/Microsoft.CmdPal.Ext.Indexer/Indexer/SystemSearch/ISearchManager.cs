// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Please do not change the function name")]
public partial interface ISearchManager
{
    string GetIndexerVersionStr();

    void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);

    void GetParameter(string pszName, [MarshalAs(UnmanagedType.Interface)] out object pValue);

    void SetParameter(string pszName, [MarshalAs(UnmanagedType.Interface)] ref object pValue);

    [return: MarshalAs(UnmanagedType.Bool)]
    bool get_UseProxy();

    string get_BypassList();

    void SetProxy(
        string pszProxyName,
        [MarshalAs(UnmanagedType.Bool)] bool fLocalBypass,
        string pszBypassList,
        uint dwPortNumber);

    [return: MarshalAs(UnmanagedType.Interface)]
    ISearchCatalogManager GetCatalog(string pszCatalog);

    string get_UserAgent();

    void put_UserAgent(string pszUserAgent);

    string get_ProxyName();

    [return: MarshalAs(UnmanagedType.Bool)]
    bool get_LocalBypass();

    uint get_PortNumber();
}
