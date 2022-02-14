
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ClassInterface((short)0)]
    [Guid("321069D2-2D7A-4AA1-9DC3-BA97CDF9AFB4")]
    [ComImport]
    public class CSearchCrawlScopeManagerClass : ISearchCrawlScopeManager, CSearchCrawlScopeManager
    {


        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void AddDefaultScopeRule([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl, [In] int fInclude, [In] uint fFollowFlags);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void AddRoot([MarshalAs(UnmanagedType.Interface), In] CSearchRoot pSearchRoot);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void RemoveRoot([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern CEnumSearchRoots EnumerateRoots();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void AddHierarchicalScope(
          [MarshalAs(UnmanagedType.LPWStr), In] string pszUrl,
          [In] int fInclude,
          [In] int fDefault,
          [In] int fOverrideChildren);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void AddUserScopeRule(
          [MarshalAs(UnmanagedType.LPWStr), In] string pszUrl,
          [In] int fInclude,
          [In] int fOverrideChildren,
          [In] uint fFollowFlags);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void RemoveScopeRule([MarshalAs(UnmanagedType.LPWStr), In] string pszRule);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern CEnumSearchScopeRules EnumerateScopeRules();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern int HasParentScopeRule([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern int HasChildScopeRule([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern int IncludedInCrawlScope([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void IncludedInCrawlScopeEx(
          [MarshalAs(UnmanagedType.LPWStr), In] string pszUrl,
          out int pfIsIncluded,
          [ComAliasName("Microsoft.Search.Interop.CLUSION_REASON")] out CLUSION_REASON pReason);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void RevertToDefaultScopes();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void SaveAll();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern int GetParentScopeVersionId([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public virtual extern void RemoveDefaultScopeRule([MarshalAs(UnmanagedType.LPWStr), In] string pszUrl);
    }
}
