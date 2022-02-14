
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [InterfaceType(1)]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF53")]
    [ComImport]
    public interface ISearchScopeRule
    {
        [DispId(1610678272)]
        string PatternOrURL { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678273)]
        int IsIncluded { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678274)]
        int IsDefault { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678275)]
        uint FollowFlags { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }
    }
}
