
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [CoClass(typeof(CEnumSearchRootsClass))]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF52")]
    [ComImport]
    public interface CEnumSearchRoots : IEnumSearchRoots
    {
    }
}
