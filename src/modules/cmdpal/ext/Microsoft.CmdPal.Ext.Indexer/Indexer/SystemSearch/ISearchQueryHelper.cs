// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1212:Property accessors should follow order", Justification = "The order of the property accessors must match the order in which the methods were defined in the vtable")]
public partial interface ISearchQueryHelper
{
    string GetConnectionString();

    uint GetQueryContentLocale();

    uint GetQueryKeywordLocale();

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetQueryTermExpansion();

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetQuerySyntax();

    string GetQueryContentProperties();

    void SetQueryContentProperties(string pszProperties);

    string GetQuerySelectColumns();

    void SetQuerySelectColumns(string pszColumns);

    string GetQueryWhereRestrictions();

    void SetQueryWhereRestrictions(string pszRestrictions);

    string GetQuerySorting();

    void SetQuerySorting(string pszSorting);

    string GenerateSQLFromUserQuery(string pszQuery);

    void WriteProperties(
      int itemID,
      uint dwNumberOfColumns,
      [MarshalAs(UnmanagedType.Interface)] ref object pColumns,
      [MarshalAs(UnmanagedType.Interface)] ref object pValues,
      [MarshalAs(UnmanagedType.Interface)] ref object pftGatherModifiedTime);

    int GetQueryMaxResults();

    void SetQueryMaxResults(int lMaxResults);
}
