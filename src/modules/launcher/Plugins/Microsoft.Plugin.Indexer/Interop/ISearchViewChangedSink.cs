
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF65")]
    [InterfaceType(1)]
    [ComImport]
    public interface ISearchViewChangedSink
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnChange([In] ref int pdwDocID, [In] ref _SEARCH_ITEM_CHANGE pChange, [In] ref int pfInView);
    }
}
