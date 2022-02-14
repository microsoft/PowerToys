
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [CoClass(typeof(CSearchManagerClass))]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
    [ComImport]
    public interface CSearchManager : ISearchManager
    {
    }
}
