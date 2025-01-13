// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

internal sealed class QueryStringBuilder
{
    private const string Select = "SELECT";
    private const string Properties = "System.ItemUrl, System.ItemNameDisplay, path, System.Search.EntryID, System.Kind, System.KindText, System.Search.GatherTime, System.Search.QueryPropertyHits";
    private const string FromIndex = "FROM SystemIndex WHERE";
    private const string ScopeFileConditions = "SCOPE='file:'";
    private const string OrderConditions = "ORDER BY System.DateModified DESC";
    private const string SelectQueryWithScope = Select + " " + Properties + " " + FromIndex + " (" + ScopeFileConditions + ")";
    private const string SelectQueryWithScopeAndOrderConditions = SelectQueryWithScope + " " + OrderConditions;

    public static string GeneratePrimingQuery() => SelectQueryWithScopeAndOrderConditions;

    public static string GenerateQuery(string searchText, uint whereId)
    {
        var queryStr = new StringBuilder(SelectQueryWithScope);

        // Filter by item name display only
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var appended = false;

            try
            {
                var parts = searchText
                    .Split([' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => $"\"{p}*\"")
                    .ToArray();

                if (parts.Length > 0)
                {
                    var joinedParts = string.Join(" AND ", parts);
                    queryStr.Append(" AND (CONTAINS(System.ItemNameDisplay, '").Append(joinedParts).Append("'))");
                    appended = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to split query string", ex);
            }

            if (!appended)
            {
                queryStr.Append(" AND (CONTAINS(System.ItemNameDisplay, '\"").Append(searchText).Append("*\"'))");
            }
        }

        // Always add reuse where to the query
        queryStr.Append(" AND ReuseWhere(")
            .Append(whereId.ToString(CultureInfo.InvariantCulture))
            .Append(") ")
            .Append(OrderConditions);

        return queryStr.ToString();
    }
}
