// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[ClassInterface((short)0)]
[Guid("B271E955-09E1-42E1-9B95-5994A534B613")]
[ComImport]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1212:Property accessors should follow order", Justification = "The order of the property accessors must match the order in which the methods were defined in the vtable")]
public class CSearchQueryHelperClass : ISearchQueryHelper, CSearchQueryHelper
{
    [DispId(1610678272)]
    public virtual extern string ConnectionString
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678273)]
    public virtual extern uint QueryContentLocale
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678275)]
    public virtual extern uint QueryKeywordLocale
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678277)]
    public virtual extern object QueryTermExpansion
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678279)]
    public virtual extern object QuerySyntax
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }

    [DispId(1610678281)]
    public virtual extern string QueryContentProperties
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: MarshalAs(UnmanagedType.LPWStr)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678283)]
    public virtual extern string QuerySelectColumns
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: MarshalAs(UnmanagedType.LPWStr)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678285)]
    public virtual extern string QueryWhereRestrictions
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: MarshalAs(UnmanagedType.LPWStr)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678287)]
    public virtual extern string QuerySorting
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: MarshalAs(UnmanagedType.LPWStr)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
    public virtual extern string GenerateSQLFromUserQuery([MarshalAs(UnmanagedType.LPWStr), In] string pszQuery);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public virtual extern void WriteProperties(
      [In] int itemID,
      [In] uint dwNumberOfColumns,
      [In] ref object pColumns,
      [In] ref object pValues,
      [In] ref object pftGatherModifiedTime);

    [DispId(1610678291)]
    public virtual extern int QueryMaxResults
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [param: In]
        set;
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        get;
    }
}
