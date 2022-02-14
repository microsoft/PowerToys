
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("A2FFDF9B-4758-4F84-B729-DF81A1A0612F")]
    [InterfaceType(1)]
    [ComImport]
    public interface ISearchPersistentItemsChangedSink
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void StartedMonitoringScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void StoppedMonitoringScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnItemsChanged(
          [In] uint dwNumberOfChanges,
          [In] ref _SEARCH_ITEM_PERSISTENT_CHANGE DataChangeEntries,
          [MarshalAs(UnmanagedType.Error)] out int hrCompletionCodes);
    }
}
