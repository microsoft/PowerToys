// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using Microsoft.CmdPal.Ext.Indexer.Native;

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

    private static readonly Guid CLSIDSearchManager = new Guid("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39");
    private static readonly Guid IIDISearchManager = new Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69");
    private const uint CLSCTXINPROCSERVER = 1;

    public static string GenerateQuery(string searchText, uint whereId)
    {
        if (queryHelper == null)
        {
            ComWrappers cw = new StrategyBasedComWrappers();
            Guid clsidSearchManager = CLSIDSearchManager;
            Guid iidSearchManager = IIDISearchManager;

            var hr = NativeMethods.CoCreateInstance(CLSIDSearchManager, IntPtr.Zero, CLSCTXINPROCSERVER, IIDISearchManager, out var searchManagerPtr);
            if (hr != 0)
            {
                throw new ArgumentException("Failed to create SearchManager instance.");
            }

            var searchManager = (CSearchManager)cw.GetOrCreateObjectForComInstance(
                searchManagerPtr, CreateObjectFlags.None);

            ISearchCatalogManager catalogManager = searchManager.GetCatalog(SystemIndex);
            queryHelper = catalogManager.GetQueryHelper();

            queryHelper.SetQuerySelectColumns(Properties);
            queryHelper.SetQueryContentProperties("System.FileName");
            queryHelper.SetQuerySorting(OrderConditions);
        }

        queryHelper.SetQueryWhereRestrictions(SelectQueryWithScope + " AND ReuseWhere(" + whereId.ToString(CultureInfo.InvariantCulture) + ")");
        return queryHelper.GenerateSQLFromUserQuery(searchText);
    }
}
