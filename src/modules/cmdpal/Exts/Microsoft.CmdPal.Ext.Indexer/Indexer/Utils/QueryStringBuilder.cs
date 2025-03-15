// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

internal sealed class QueryStringBuilder
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
        if (queryHelper == null)
        {
            var searchManager = new CSearchManager();
            ISearchCatalogManager catalogManager = searchManager.GetCatalog(SystemIndex);
            queryHelper = catalogManager.GetQueryHelper();

            queryHelper.QuerySelectColumns = Properties;
            queryHelper.QueryContentProperties = "System.FileName";
            queryHelper.QuerySorting = OrderConditions;
        }

        queryHelper.QueryWhereRestrictions = "AND " + ScopeFileConditions + "AND ReuseWhere(" + whereId.ToString(CultureInfo.InvariantCulture) + ")";
        return queryHelper.GenerateSQLFromUserQuery(searchText);
    }
}
