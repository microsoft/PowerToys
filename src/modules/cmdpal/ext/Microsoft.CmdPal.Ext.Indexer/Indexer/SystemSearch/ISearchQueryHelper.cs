// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
[InterfaceType(1)]
[GeneratedComInterface]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1212:Property accessors should follow order", Justification = "The order of the property accessors must match the order in which the methods were defined in the vtable")]
public partial interface ISearchQueryHelper
{
    [DispId(1610678272)]
    string ConnectionString
    {
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678273)]
    uint QueryContentLocale
    {
    
        set;
    
        get;
    }

    [DispId(1610678275)]
    uint QueryKeywordLocale
    {
    
        set;
    
        get;
    }

    [DispId(1610678277)]
    object QueryTermExpansion
    {
    
        set;
    
        get;
    }

    [DispId(1610678279)]
    object QuerySyntax
    {
    
        set;
    
        get;
    }

    [DispId(1610678281)]
    string QueryContentProperties
    {
    
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678283)]
    string QuerySelectColumns
    {
    
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678285)]
    string QueryWhereRestrictions
    {
    
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [DispId(1610678287)]
    string QuerySorting
    {
    
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
    
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }


    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GenerateSQLFromUserQuery([MarshalAs(UnmanagedType.LPWStr), In] string pszQuery);


    void WriteProperties(
      [In] int itemID,
      [In] uint dwNumberOfColumns,
      [In] ref object pColumns,
      [In] ref object pValues,
      [In] ref object pftGatherModifiedTime);

    [DispId(1610678291)]
    int QueryMaxResults
    {
    
        set;
    
        get;
    }
}
