// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Please do not change the function name")]
public partial interface ISearchCatalogManager
{
    string get_Name();

    void GetParameter(string pszName, out IntPtr pValue);

    void SetParameter(string pszName, ref IntPtr pValue);

    void GetCatalogStatus(out uint pdwStatus, out uint pdwPausedReason);

    void Reset();

    void Reindex();

    void ReindexMatchingURLs(string pszPattern);

    void ReindexSearchRoot(string pszRoot);

    void put_ConnectTimeout(uint dwTimeout);

    uint get_ConnectTimeout();

    void put_DataTimeout(uint dwTimeout);

    uint get_DataTimeout();

    uint NumberOfItems();

    void NumberOfItemsToIndex(out uint plIncrementalCount, out uint plNotificationQueue, out uint plHighPriorityQueue);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string URLBeingIndexed();

    void GetURLIndexingState(string pszURL, out uint pdwState);

    IntPtr GetPersistentItemsChangedSink();

    void RegisterViewForNotification(string pszView, IntPtr pViewNotify, out uint pdwCookie);

    void GetItemsChangedSink(
        IntPtr pISearchNotifyInlineSite,
        in Guid riid,
        out IntPtr ppv,
        out Guid pGUIDCatalogResetSignature,
        out Guid pGUIDCheckPointSignature,
        out uint pdwLastCheckPointNumber);

    void UnregisterViewForNotification(uint dwCookie);

    void SetExtensionClusion(string pszExtension, [MarshalAs(UnmanagedType.Bool)] bool fExclude);

    IntPtr EnumerateExcludedExtensions();

    ISearchQueryHelper GetQueryHelper();

    [return: MarshalAs(UnmanagedType.Bool)]
    bool get_DiacriticSensitivity();

    void put_DiacriticSensitivity([MarshalAs(UnmanagedType.Bool)] bool fDiacriticSensitive);

    IntPtr GetCrawlScopeManager();
}
