// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

internal static class QueryStringBuilder
{
    private const string Properties = "System.ItemUrl, System.ItemNameDisplay, path, System.Search.EntryID, System.Kind, System.KindText";
    private const string SystemIndex = "SystemIndex";
    private const string ScopeFileConditions = "SCOPE='file:'";
    private const string OrderConditions = "System.DateModified DESC";
    private const string ContentProperties = "System.FileName";

    public static SearchSqlQueryPlan GenerateQueryPlan(string searchText)
    {
        var expandedQuery = ImplicitWildcardQueryBuilder.BuildExpandedQuery(searchText);
        var primarySqlQuery = expandedQuery.HasPrimaryRestriction
            ? BuildQuery(expandedQuery.StructuredSearchText, expandedQuery.PrimaryRestriction!)
            : GenerateQuery(searchText);

        var fallbackSqlQuery = expandedQuery.HasFallbackRestriction
            ? BuildQuery(expandedQuery.StructuredSearchText, expandedQuery.FallbackRestriction!)
            : null;

        return new SearchSqlQueryPlan(primarySqlQuery, fallbackSqlQuery);
    }

    private static string GenerateQuery(string searchText, string? additionalRestrictions = null)
    {
        var queryHelper = CreateQueryHelper();

        queryHelper.SetQuerySelectColumns(Properties);
        queryHelper.SetQueryContentProperties(ContentProperties);
        queryHelper.SetQuerySorting(OrderConditions);
        queryHelper.SetQuerySyntax(SEARCH_QUERY_SYNTAX.SEARCH_ADVANCED_QUERY_SYNTAX);

        var restrictions = $"AND {ScopeFileConditions}";
        if (!string.IsNullOrWhiteSpace(additionalRestrictions))
        {
            restrictions += $" AND ({additionalRestrictions})";
        }

        queryHelper.SetQueryWhereRestrictions(restrictions);
        return queryHelper.GenerateSQLFromUserQuery(searchText);
    }

    private static string BuildQuery(string? structuredSearchText, string restriction)
    {
        return string.IsNullOrWhiteSpace(structuredSearchText)
            ? GenerateRestrictionOnlyQuery(restriction)
            : GenerateQuery(structuredSearchText, restriction);
    }

    private static string GenerateRestrictionOnlyQuery(string restriction)
    {
        return $"""
                SELECT {Properties}
                FROM {SystemIndex}
                WHERE {ScopeFileConditions} AND ({restriction})
                ORDER BY {OrderConditions}
                """;
    }

    private static ISearchQueryHelper CreateQueryHelper()
    {
        ISearchManager searchManager;

        try
        {
            searchManager = ComHelper.CreateComInstance<ISearchManager>(ref Unsafe.AsRef(in CLSID.SearchManager), CLSCTX.LocalServer);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to create searchManager.", ex);
            throw;
        }

        var catalogManager = searchManager.GetCatalog(SystemIndex);
        if (catalogManager is null)
        {
            throw new ArgumentException($"Failed to get catalog manager for {SystemIndex}");
        }

        var queryHelper = catalogManager.GetQueryHelper();
        if (queryHelper is null)
        {
            throw new ArgumentException("Failed to get query helper from catalog manager");
        }

        return queryHelper;
    }
}

internal readonly record struct SearchSqlQueryPlan(string PrimarySqlQuery, string? FallbackSqlQuery)
{
    public bool HasFallback => !string.IsNullOrWhiteSpace(FallbackSqlQuery);
}
