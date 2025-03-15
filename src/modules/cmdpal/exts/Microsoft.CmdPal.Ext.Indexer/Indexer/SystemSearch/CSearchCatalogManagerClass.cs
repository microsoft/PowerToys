// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[ComConversionLoss]
[Guid("AAB49DD5-AD0B-40AE-B654-AE8976BF6BD2")]
[ClassInterface((short)0)]
[ComImport]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1212:Property accessors should follow order", Justification = "The order of the property accessors must match the order in which the methods were defined in the vtable")]
public class CSearchCatalogManagerClass : ISearchCatalogManager, CSearchCatalogManager
{
    [DispId(1610678272)]
    public virtual extern string Name
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern IntPtr GetParameter([MarshalAs(UnmanagedType.LPWStr), In] string pszName);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void SetParameter([MarshalAs(UnmanagedType.LPWStr), In] string pszName, [In] ref object pValue);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void GetCatalogStatus(
      out object pStatus,
      out object pPausedReason);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void Reset();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void Reindex();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void ReindexMatchingURLs([MarshalAs(UnmanagedType.LPWStr), In] string pszPattern);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void ReindexSearchRoot([MarshalAs(UnmanagedType.LPWStr), In] string pszRoot);

    [DispId(1610678280)]
    public virtual extern uint ConnectTimeout
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678282)]
    public virtual extern uint DataTimeout
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern int NumberOfItems();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void NumberOfItemsToIndex(
      out int plIncrementalCount,
      out int plNotificationQueue,
      out int plHighPriorityQueue);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
    public virtual extern string URLBeingIndexed();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern uint GetURLIndexingState([MarshalAs(UnmanagedType.LPWStr), In] string psz);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    public virtual extern object GetPersistentItemsChangedSink();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void RegisterViewForNotification(
      [MarshalAs(UnmanagedType.LPWStr), In] string pszView,
      [MarshalAs(UnmanagedType.Interface), In] object pViewChangedSink,
      out uint pdwCookie);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void GetItemsChangedSink(
      [MarshalAs(UnmanagedType.Interface), In] object pISearchNotifyInlineSite,
      [In] ref Guid riid,
      out IntPtr ppv,
      out Guid pGUIDCatalogResetSignature,
      out Guid pGUIDCheckPointSignature,
      out uint pdwLastCheckPointNumber);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void UnregisterViewForNotification([In] uint dwCookie);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void SetExtensionClusion([MarshalAs(UnmanagedType.LPWStr), In] string pszExtension, [In] int fExclude);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    public virtual extern object EnumerateExcludedExtensions();

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    public virtual extern CSearchQueryHelper GetQueryHelper();

    [DispId(1610678295)]
    public virtual extern int DiacriticSensitivity
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.Interface)]
    public virtual extern object GetCrawlScopeManager();
}
