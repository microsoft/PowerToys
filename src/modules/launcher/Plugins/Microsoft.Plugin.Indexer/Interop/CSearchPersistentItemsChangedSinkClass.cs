
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("D0F268B5-EA7A-4B35-BF2F-E1A091B80D51")]
    [ClassInterface((short)0)]
    [ComImport]
    public class CSearchPersistentItemsChangedSinkClass :
    ISearchPersistentItemsChangedSink,
    CSearchPersistentItemsChangedSink
    {


        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void StartedMonitoringScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void StoppedMonitoringScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void OnItemsChanged(
          [In] uint dwNumberOfChanges,
          [In] ref _SEARCH_ITEM_PERSISTENT_CHANGE DataChangeEntries,
          [MarshalAs(UnmanagedType.Error)] out int hrCompletionCodes);
    }
}
