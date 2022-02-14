
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF53")]
    [CoClass(typeof(CSearchScopeRuleClass))]
    [ComImport]
    public interface CSearchScopeRule : ISearchScopeRule
    {
    }
}
