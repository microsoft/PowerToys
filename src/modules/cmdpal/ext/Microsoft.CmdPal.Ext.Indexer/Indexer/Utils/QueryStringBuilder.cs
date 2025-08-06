// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

internal sealed partial class QueryStringBuilder
{
    private const string Properties = "System.ItemUrl, System.ItemNameDisplay, path, System.Search.EntryID, System.Kind, System.KindText";
    private const string SystemIndex = "SystemIndex";
    private const string ScopeFileConditions = "SCOPE='file:'";
    private const string OrderConditions = "System.DateModified DESC";
    private const string SelectQueryWithScope = "SELECT " + Properties + " FROM " + SystemIndex + " WHERE (" + ScopeFileConditions + ")";
    private const string SelectQueryWithScopeAndOrderConditions = SelectQueryWithScope + " ORDER BY " + OrderConditions;

    private static ISearchQueryHelper queryHelper;

    public static string GeneratePrimingQuery() => SelectQueryWithScopeAndOrderConditions;

    public static string GenerateQuery(string searchText, uint whereId)
    {
        if (queryHelper is null)
        {
            ISearchManager searchManager;

            try
            {
                searchManager = ComHelper.CreateComInstance<ISearchManager>(ref Unsafe.AsRef(in CLSID.SearchManager), CLSCTX.LocalServer);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create searchManager. ex: {ex.Message}");
                throw;
            }

            ISearchCatalogManager catalogManager = searchManager.GetCatalog(SystemIndex);
            if (catalogManager is null)
            {
                throw new ArgumentException($"Failed to get catalog manager for {SystemIndex}");
            }

            queryHelper = catalogManager.GetQueryHelper();
            if (queryHelper is null)
            {
                throw new ArgumentException("Failed to get query helper from catalog manager");
            }

            queryHelper.SetQuerySelectColumns(Properties);
            queryHelper.SetQueryContentProperties("System.FileName");
            queryHelper.SetQuerySorting(OrderConditions);
        }

        queryHelper.SetQueryWhereRestrictions("AND " + ScopeFileConditions + "AND ReuseWhere(" + whereId.ToString(CultureInfo.InvariantCulture) + ")");
        return queryHelper.GenerateSQLFromUserQuery(searchText);
    }
}
