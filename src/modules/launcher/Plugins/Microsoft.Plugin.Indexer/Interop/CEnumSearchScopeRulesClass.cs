
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("3379F9F7-F805-4827-9CF6-03DB41F297DA")]
    [ClassInterface((short)0)]
    [ComImport]
    public class CEnumSearchScopeRulesClass : IEnumSearchScopeRules, CEnumSearchScopeRules
    {


        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out CSearchScopeRule pprgelt, [In, Out] ref uint pceltFetched);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Skip([In] uint celt);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void Reset();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern CEnumSearchScopeRules Clone();
    }
}
