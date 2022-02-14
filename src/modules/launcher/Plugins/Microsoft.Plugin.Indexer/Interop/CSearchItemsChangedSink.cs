
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [CoClass(typeof(CSearchItemsChangedSinkClass))]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF58")]
    [ComImport]
    public interface CSearchItemsChangedSink : ISearchItemsChangedSink
    {
    }
}
