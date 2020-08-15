// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Search.Interop;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class WindowsSearchAPI
    {
        public bool DisplayHiddenFiles { get; set; }

        private readonly ISearch windowsIndexerSearch;

        private const uint _fileAttributeHidden = 0x2;
        private static readonly Regex _likeRegex = new Regex(@"[^\s(]+\s+LIKE\s+'([^']|'')*'\s+OR\s+", RegexOptions.Compiled);

        public WindowsSearchAPI(ISearch windowsIndexerSearch, bool displayHiddenFiles = false)
        {
            this.windowsIndexerSearch = windowsIndexerSearch;
            DisplayHiddenFiles = displayHiddenFiles;
        }

        public List<SearchResult> ExecuteQuery(ISearchQueryHelper queryHelper, string keyword, bool isFullQuery = false)
        {
            if (queryHelper == null)
            {
                throw new ArgumentNullException(paramName: nameof(queryHelper));
            }

            List<SearchResult> results = new List<SearchResult>();

            // Generate SQL from our parameters, converting the userQuery from AQS->WHERE clause
            string sqlQuery = queryHelper.GenerateSQLFromUserQuery(keyword);
            var simplifiedQuery = SimplifyQuery(sqlQuery);

            if (!isFullQuery)
            {
                sqlQuery = simplifiedQuery;
            }
            else if (simplifiedQuery.Equals(sqlQuery, StringComparison.CurrentCultureIgnoreCase))
            {
                // if a full query is requested but there is no difference between the queries, return empty results
                return results;
            }

            // execute the command, which returns the results as an OleDBResults.
            List<OleDBResult> oleDBResults = windowsIndexerSearch.Query(queryHelper.ConnectionString, sqlQuery);

            // Loop over all records from the database
            foreach (OleDBResult oleDBResult in oleDBResults)
            {
                if (oleDBResult.FieldData[0] == DBNull.Value || oleDBResult.FieldData[1] == DBNull.Value)
                {
                    continue;
                }

                var uri_path = new Uri((string)oleDBResult.FieldData[0]);
                var result = new SearchResult
                {
                    Path = uri_path.LocalPath,
                    Title = (string)oleDBResult.FieldData[1],
                };

                results.Add(result);
            }

            return results;
        }

        public static void ModifyQueryHelper(ref ISearchQueryHelper queryHelper, string pattern)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException(paramName: nameof(pattern));
            }

            if (queryHelper == null)
            {
                throw new ArgumentNullException(paramName: nameof(queryHelper));
            }

            // convert file pattern if it is not '*'. Don't create restriction for '*' as it includes all files.
            if (pattern != "*")
            {
                pattern = pattern.Replace("*", "%", StringComparison.InvariantCulture);
                pattern = pattern.Replace("?", "_", StringComparison.InvariantCulture);

                if (pattern.Contains("%", StringComparison.InvariantCulture) || pattern.Contains("_", StringComparison.InvariantCulture))
                {
                    queryHelper.QueryWhereRestrictions += " AND System.FileName LIKE '" + pattern + "' ";
                }
                else
                {
                    // if there are no wildcards we can use a contains which is much faster as it uses the index
                    queryHelper.QueryWhereRestrictions += " AND Contains(System.FileName, '" + pattern + "') ";
                }
            }
        }

        public static void InitQueryHelper(out ISearchQueryHelper queryHelper, int maxCount, bool displayHiddenFiles)
        {
            // This uses the Microsoft.Search.Interop assembly
            CSearchManager manager = new CSearchManager();

            // SystemIndex catalog is the default catalog in Windows
            ISearchCatalogManager catalogManager = manager.GetCatalog("SystemIndex");

            // Get the ISearchQueryHelper which will help us to translate AQS --> SQL necessary to query the indexer
            queryHelper = catalogManager.GetQueryHelper();

            // Set the number of results we want. Don't set this property if all results are needed.
            queryHelper.QueryMaxResults = maxCount;

            // Set list of columns we want to display, getting the path presently
            queryHelper.QuerySelectColumns = "System.ItemUrl, System.FileName, System.FileAttributes";

            // Set additional query restriction
            queryHelper.QueryWhereRestrictions = "AND scope='file:'";

            if (!displayHiddenFiles)
            {
                // https://docs.microsoft.com/en-us/windows/win32/search/all-bitwise
                queryHelper.QueryWhereRestrictions += " AND System.FileAttributes <> SOME BITWISE " + _fileAttributeHidden;
            }

            // To filter based on title for now
            queryHelper.QueryContentProperties = "System.FileName";

            // Set sorting order
            queryHelper.QuerySorting = "System.DateModified DESC";
        }

        public IEnumerable<SearchResult> Search(string keyword, bool isFullQuery = false, string pattern = "*", int maxCount = 30)
        {
            ISearchQueryHelper queryHelper;
            InitQueryHelper(out queryHelper, maxCount, DisplayHiddenFiles);
            ModifyQueryHelper(ref queryHelper, pattern);
            return ExecuteQuery(queryHelper, keyword, isFullQuery);
        }

        public static string SimplifyQuery(string sqlQuery)
        {
            return _likeRegex.Replace(sqlQuery, string.Empty);
        }
    }
}
