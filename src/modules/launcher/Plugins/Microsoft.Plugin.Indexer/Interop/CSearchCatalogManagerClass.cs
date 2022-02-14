
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ComConversionLoss]
    [Guid("AAB49DD5-AD0B-40AE-B654-AE8976BF6BD2")]
    [ClassInterface((short)0)]
    [ComImport]
    public class CSearchCatalogManagerClass : ISearchCatalogManager, CSearchCatalogManager
    {


        [DispId(1610678272)]
        public virtual extern string Name { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern IntPtr GetParameter([MarshalAs(UnmanagedType.LPWStr), In] string pszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void SetParameter([MarshalAs(UnmanagedType.LPWStr), In] string pszName, [In] ref tag_inner_PROPVARIANT pValue);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void GetCatalogStatus(
          out _CatalogStatus pStatus,
          out _CatalogPausedReason pPausedReason);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Reset();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Reindex();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void ReindexMatchingURLs([MarshalAs(UnmanagedType.LPWStr), In] string pszPattern);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void ReindexSearchRoot([MarshalAs(UnmanagedType.LPWStr), In] string pszRootURL);

        [DispId(1610678280)]
        public virtual extern uint ConnectTimeout { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678282)]
        public virtual extern uint DataTimeout { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

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
        public virtual extern uint GetURLIndexingState([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern CSearchPersistentItemsChangedSink GetPersistentItemsChangedSink();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void RegisterViewForNotification(
          [MarshalAs(UnmanagedType.LPWStr), In] string pszView,
          [MarshalAs(UnmanagedType.Interface), In] ISearchViewChangedSink pViewChangedSink,
          out uint pdwCookie);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void GetItemsChangedSink(
          [MarshalAs(UnmanagedType.Interface), In] ISearchNotifyInlineSite pISearchNotifyInlineSite,
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
        public virtual extern IEnumString EnumerateExcludedExtensions();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern CSearchQueryHelper GetQueryHelper();

        [DispId(1610678295)]
        public virtual extern int DiacriticSensitivity { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern CSearchCrawlScopeManager GetCrawlScopeManager();
    }
}
