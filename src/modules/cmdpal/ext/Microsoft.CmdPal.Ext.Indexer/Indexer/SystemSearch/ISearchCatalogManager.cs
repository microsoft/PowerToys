// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[GeneratedComInterface]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Please do not change the function name")]
public partial interface ISearchCatalogManager
{
    [return: MarshalAs(UnmanagedType.BStr)]
    string get_Name();

    void GetCatalogStatus(out uint pdwStatus, out uint pdwPausedReason);

    void Reset();

    void Reindex();

    void ReindexSearchRoot([MarshalAs(UnmanagedType.LPWStr)] string pszRoot);

    IntPtr GetCrawlScopeManager();

    [return: MarshalAs(UnmanagedType.Interface)]
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    ISearchQueryHelper GetQueryHelper();

    uint NumberOfItems();

    uint NumberOfItemsToIndex();

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string URLBeingIndexed();

    IntPtr GetItemsChangedSink();

    IntPtr GetPersistentItemsChangedSink();

    void RegisterViewForNotification([MarshalAs(UnmanagedType.LPWStr)] string pszView, IntPtr pViewNotify, out uint pdwCookie);

    void UnregisterViewForNotification(uint dwCookie);

    void GetURLIndexingState([MarshalAs(UnmanagedType.LPWStr)] string pszURL, out uint pdwState);

    uint get_DataTimeout();

    void put_DataTimeout(uint dwTimeout);

    uint get_ConnectTimeout();

    void put_ConnectTimeout(uint dwTimeout);

    void EnumerateExcludedExtensions();

    void SetExtensionClusion([MarshalAs(UnmanagedType.LPWStr)] string pszExtension, [MarshalAs(UnmanagedType.Bool)] bool fExclude);

    [return: MarshalAs(UnmanagedType.Bool)]
    bool get_DiacriticSensitivity();

    void put_DiacriticSensitivity([MarshalAs(UnmanagedType.Bool)] bool fDiacriticSensitive);

    void GetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, out IntPtr pValue);

    void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, ref IntPtr pValue);

    void ReindexMatchingURLs([MarshalAs(UnmanagedType.LPWStr)] string pszPattern);
}
