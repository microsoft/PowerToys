
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [Guid("E63DE750-3BD7-4BE5-9C84-6B4281988C44")]
    [ClassInterface((short)0)]
    [TypeLibType(2)]
    [ComImport]
    public class CSearchScopeRuleClass : ISearchScopeRule, CSearchScopeRule
    {


        [DispId(1610678272)]
        public virtual extern string PatternOrURL { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678273)]
        public virtual extern int IsIncluded { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678274)]
        public virtual extern int IsDefault { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678275)]
        public virtual extern uint FollowFlags { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }
    }
}
