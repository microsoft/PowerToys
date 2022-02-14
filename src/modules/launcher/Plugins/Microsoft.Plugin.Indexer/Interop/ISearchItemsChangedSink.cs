
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF58")]
    [InterfaceType(1)]
    [ComImport]
    public interface ISearchItemsChangedSink
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void StartedMonitoringScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void StoppedMonitoringScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnItemsChanged(
          [In] uint dwNumberOfChanges,
          [In] ref _SEARCH_ITEM_CHANGE rgDataChangeEntries,
          out uint rgdwDocIds,
          [MarshalAs(UnmanagedType.Error)] out int rghrCompletionCodes);
    }
}
