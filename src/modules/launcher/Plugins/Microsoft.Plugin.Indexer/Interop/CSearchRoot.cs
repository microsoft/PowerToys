
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("04C18CCF-1F57-4CBD-88CC-3900F5195CE3")]
    [CoClass(typeof(CSearchRootClass))]
    [ComImport]
    public interface CSearchRoot : ISearchRoot
    {
    }
}
