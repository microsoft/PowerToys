
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
    [CoClass(typeof(CSearchQueryHelperClass))]
    [ComImport]
    public interface CSearchQueryHelper : ISearchQueryHelper
    {
    }
}
