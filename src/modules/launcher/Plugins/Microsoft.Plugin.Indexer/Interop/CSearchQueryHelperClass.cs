
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ClassInterface((short)0)]
    [Guid("B271E955-09E1-42E1-9B95-5994A534B613")]
    [ComImport]
    public class CSearchQueryHelperClass : ISearchQueryHelper, CSearchQueryHelper
    {


        [DispId(1610678272)]
        public virtual extern string ConnectionString { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678273)]
        public virtual extern uint QueryContentLocale { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678275)]
        public virtual extern uint QueryKeywordLocale { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678277)]
        public virtual extern _SEARCH_TERM_EXPANSION QueryTermExpansion { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678279)]
        public virtual extern _SEARCH_QUERY_SYNTAX QuerySyntax { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }

        [DispId(1610678281)]
        public virtual extern string QueryContentProperties { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678283)]
        public virtual extern string QuerySelectColumns { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678285)]
        public virtual extern string QueryWhereRestrictions { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [DispId(1610678287)]
        public virtual extern string QuerySorting { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: MarshalAs(UnmanagedType.LPWStr), In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][return: MarshalAs(UnmanagedType.LPWStr)] get; }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        public virtual extern string GenerateSQLFromUserQuery([MarshalAs(UnmanagedType.LPWStr), In] string pszQuery);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void WriteProperties(
          [In] int itemID,
          [In] uint dwNumberOfColumns,
          [In] ref _tagpropertykey pColumns,
          [In] ref _SEARCH_COLUMN_PROPERTIES pValues,
          [In] ref _FILETIME pftGatherModifiedTime);

        [DispId(1610678291)]
        public virtual extern int QueryMaxResults { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)][param: In] set; [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] get; }
    }
}
