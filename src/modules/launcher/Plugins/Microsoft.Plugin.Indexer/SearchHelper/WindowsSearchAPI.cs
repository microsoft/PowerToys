// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Plugin.Indexer.Interop;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class WindowsSearchAPI
    {
        public bool DisplayHiddenFiles { get; set; }

        private readonly ISearch windowsIndexerSearch;

        private const uint _fileAttributeHidden = 0x2;

        public WindowsSearchAPI(ISearch windowsIndexerSearch, bool displayHiddenFiles = false)
        {
            this.windowsIndexerSearch = windowsIndexerSearch;
            DisplayHiddenFiles = displayHiddenFiles;
        }

        public List<SearchResult> ExecuteQuery(ISearchQueryHelper queryHelper, string keyword)
        {
            ArgumentNullException.ThrowIfNull(queryHelper);

            List<SearchResult> results = new List<SearchResult>();

            // Generate SQL from our parameters, converting the userQuery from AQS->WHERE clause
            string sqlQuery = queryHelper.GenerateSQLFromUserQuery(keyword);

            // execute the command, which returns the results as an OleDBResults.
            List<OleDBResult> oleDBResults = windowsIndexerSearch.Query(queryHelper.ConnectionString, sqlQuery);

            // Loop over all records from the database
            foreach (OleDBResult oleDBResult in oleDBResults)
            {
                if (oleDBResult.FieldData[0] == DBNull.Value || oleDBResult.FieldData[1] == DBNull.Value)
                {
                    continue;
                }

                // # is URI syntax for the fragment component, need to be encoded so LocalPath returns complete path
                // Using OrdinalIgnoreCase since this is internal and used with symbols
                var string_path = ((string)oleDBResult.FieldData[0]).Replace("#", "%23", StringComparison.OrdinalIgnoreCase);

                if (!Uri.TryCreate(string_path, UriKind.RelativeOrAbsolute, out Uri uri_path))
                {
                    Log.Warn($"Failed to parse URI '${string_path}'", typeof(WindowsSearchAPI));
                    continue;
                }

                var result = new SearchResult
                {
                    Path = uri_path.LocalPath,
                    Title = (string)oleDBResult.FieldData[1],
                };

                results.Add(result);
            }

            return results;
        }

        public static void ModifyQueryHelper(ref ISearchQueryHelper queryHelper, string pattern, List<string> excludedPatterns = null)
        {
            ArgumentNullException.ThrowIfNull(pattern);

            ArgumentNullException.ThrowIfNull(queryHelper);

            // convert file pattern if it is not '*'. Don't create restriction for '*' as it includes all files.
            if (pattern != "*")
            {
                // Using Ordinal since these are internal and used with symbols
                pattern = pattern.Replace("*", "%", StringComparison.Ordinal);
                pattern = pattern.Replace("?", "_", StringComparison.Ordinal);

                if (pattern.Contains('%', StringComparison.Ordinal) || pattern.Contains('_', StringComparison.Ordinal))
                {
                    queryHelper.QueryWhereRestrictions += " AND System.FileName LIKE '" + pattern + "' ";
                }
                else
                {
                    // if there are no wildcards we can use a contains which is much faster as it uses the index
                    queryHelper.QueryWhereRestrictions += " AND Contains(System.FileName, '" + pattern + "') ";
                }
            }

            if (excludedPatterns != null)
            {
                foreach (string p in excludedPatterns)
                {
                    if (p == string.Empty)
                    {
                        continue;
                    }

                    var excludedPattern = p;

                    excludedPattern = excludedPattern.Replace("\\", "/", StringComparison.Ordinal);

                    if (excludedPattern.Contains('*', StringComparison.Ordinal) || excludedPattern.Contains('?', StringComparison.Ordinal))
                    {
                        excludedPattern = excludedPattern
                            .Replace("%", "[%]", StringComparison.Ordinal)
                            .Replace("_", "[_]", StringComparison.Ordinal)
                            .Replace("*", "%", StringComparison.Ordinal)
                            .Replace("?", "_", StringComparison.Ordinal);
                        queryHelper.QueryWhereRestrictions += " AND System.ItemUrl NOT LIKE '%" + excludedPattern + "%' ";
                    }
                    else
                    {
                        queryHelper.QueryWhereRestrictions += " AND NOT Contains(System.ItemUrl, '" + excludedPattern + "') ";
                    }
                }
            }
        }

        public static void InitQueryHelper(out ISearchQueryHelper queryHelper, ISearchManager manager, int maxCount, bool displayHiddenFiles)
        {
            ArgumentNullException.ThrowIfNull(manager);

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
                // https://learn.microsoft.com/windows/win32/search/all-bitwise
                queryHelper.QueryWhereRestrictions += " AND System.FileAttributes <> SOME BITWISE " + _fileAttributeHidden;
            }

            // To filter based on title for now
            queryHelper.QueryContentProperties = "System.FileName";

            // Set sorting order
            queryHelper.QuerySorting = "System.DateModified DESC";
        }

        public IEnumerable<SearchResult> Search(string keyword, ISearchManager manager, string pattern = "*", List<string> excludedPatterns = null, int maxCount = 30)
        {
            ArgumentNullException.ThrowIfNull(manager);
            excludedPatterns ??= new List<string>();

            ISearchQueryHelper queryHelper;
            InitQueryHelper(out queryHelper, manager, maxCount, DisplayHiddenFiles);
            ModifyQueryHelper(ref queryHelper, pattern, excludedPatterns);
            return ExecuteQuery(queryHelper, keyword);
        }
    }
}
