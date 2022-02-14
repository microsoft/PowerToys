
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
    [InterfaceType(1)]
    [ComImport]
    public interface ISearchQueryHelper
    {
        [DispId(1610678272)]
        string ConnectionString { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678273)]
        uint QueryContentLocale { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678275)]
        uint QueryKeywordLocale { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678277)]
        _SEARCH_TERM_EXPANSION QueryTermExpansion { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678279)]
        _SEARCH_QUERY_SYNTAX QuerySyntax { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678281)]
        string QueryContentProperties { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678283)]
        string QuerySelectColumns { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678285)]
        string QueryWhereRestrictions { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678287)]
        string QuerySorting { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GenerateSQLFromUserQuery([MarshalAs(UnmanagedType.LPWStr), In] string pszQuery);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void WriteProperties(
          [In] int itemID,
          [In] uint dwNumberOfColumns,
          [In] ref _tagpropertykey pColumns,
          [In] ref _SEARCH_COLUMN_PROPERTIES pValues,
          [In] ref _FILETIME pftGatherModifiedTime);

        [DispId(1610678291)]
        int QueryMaxResults { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }
    }
}
