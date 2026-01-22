// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "I don't want to change the name")]
public partial interface ISearchQueryHelper
{
    string GetConnectionString();

    void SetQueryContentLocale(int lcid);

    uint GetQueryContentLocale();

    void SetQueryKeywordLocale(int lcid);

    uint GetQueryKeywordLocale();

    void SetQueryTermExpansion(SEARCH_TERM_EXPANSION expandTerms);

    void GetQueryTermExpansion(out SEARCH_TERM_EXPANSION pExpandTerms);

    void SetQuerySyntax(SEARCH_QUERY_SYNTAX querySyntax);

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetQuerySyntax();

    void SetQueryContentProperties(string pszContentProperties);

    string GetQueryContentProperties();

    void SetQuerySelectColumns(string pszColumns);

    string GetQuerySelectColumns();

    void SetQueryWhereRestrictions(string pszRestrictions);

    string GetQueryWhereRestrictions();

    void SetQuerySorting(string pszSorting);

    string GetQuerySorting();

    string GenerateSQLFromUserQuery(string pszQuery);

    void WriteProperties(
      int itemID,
      uint dwNumberOfColumns,
      [MarshalAs(UnmanagedType.Interface)] ref object pColumns,
      [MarshalAs(UnmanagedType.Interface)] ref object pValues,
      [MarshalAs(UnmanagedType.Interface)] ref object pftGatherModifiedTime);

    void SetQueryMaxResults(int lMaxResults);

    int GetQueryMaxResults();
}

public enum SEARCH_TERM_EXPANSION
{
    SEARCH_TERM_NO_EXPANSION,
    SEARCH_TERM_PREFIX_ALL,
    SEARCH_TERM_STEM_ALL,
}

public enum SEARCH_QUERY_SYNTAX
{
    SEARCH_NO_QUERY_SYNTAX,
    SEARCH_ADVANCED_QUERY_SYNTAX,
    SEARCH_NATURAL_QUERY_SYNTAX,
}
