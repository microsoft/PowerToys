
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [CoClass(typeof(CSearchCatalogManagerClass))]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50")]
    [ComImport]
    public interface CSearchCatalogManager : ISearchCatalogManager
    {
    }
}
