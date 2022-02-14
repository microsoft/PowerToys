
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [CoClass(typeof(CSearchPersistentItemsChangedSinkClass))]
    [Guid("A2FFDF9B-4758-4F84-B729-DF81A1A0612F")]
    [ComImport]
    public interface CSearchPersistentItemsChangedSink : ISearchPersistentItemsChangedSink
    {
    }
}
