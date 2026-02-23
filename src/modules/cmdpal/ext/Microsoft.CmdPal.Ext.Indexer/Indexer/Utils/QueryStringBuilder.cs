// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

    private static ISearchQueryHelper queryHelper;

    public static string GenerateQuery(string searchText)
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

            var catalogManager = searchManager.GetCatalog(SystemIndex);
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

        queryHelper.SetQueryWhereRestrictions($"AND {ScopeFileConditions}");

        // Strip characters that Windows Search treats as noise words (e.g. &, @, #)
        // to avoid QUERY_E_ALLNOISE errors. Keep only alphanumeric characters,
        // whitespace, dots, hyphens, and underscores which are valid in file names.
        var sanitized = Regex.Replace(searchText, @"[^\w\s.\-]", " ");
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(sanitized))
        {
            return string.Empty;
        }

        return queryHelper.GenerateSQLFromUserQuery(sanitized);
    }
}
