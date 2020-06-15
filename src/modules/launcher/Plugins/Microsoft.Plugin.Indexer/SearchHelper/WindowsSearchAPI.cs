using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Diagnostics;
using Microsoft.Search.Interop;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class WindowsSearchAPI
    {
        public List<DBResults> WDSResults;
        public bool DisplayHiddenFiles = false;
        public UInt32 FILE_ATTRIBUTE_HIDDEN = 0x2;
        private readonly object _lock = new object();
        private WindowsIndexerSearch WindowsIndexerSearch;

        public WindowsSearchAPI(WindowsIndexerSearch windowsIndexerSearch)
        {
            this.WindowsIndexerSearch = windowsIndexerSearch;
        }

        public List<SearchResult> ExecuteQuery(ISearchQueryHelper queryHelper, string keyword)
        {
            List<SearchResult> _Result = new List<SearchResult>();

            // Generate SQL from our parameters, converting the userQuery from AQS->WHERE clause
            string sqlQuery = queryHelper.GenerateSQLFromUserQuery(keyword);

            // execute the command, which returns the results as an OleDbDataReader.
            WDSResults = WindowsIndexerSearch.Query(queryHelper.ConnectionString, sqlQuery);

            // Lopp over all records from the database
            foreach(IDataRecord data in WDSResults)
            {
                if (data.GetValue(0) != DBNull.Value && data.GetValue(1) != DBNull.Value && data.GetValue(2) != DBNull.Value)
                {
                    UInt32 fileAttributes = (UInt32)data.GetInt64(2);
                    bool isFileHidden = (fileAttributes & FILE_ATTRIBUTE_HIDDEN) == FILE_ATTRIBUTE_HIDDEN;

                    if (DisplayHiddenFiles || !isFileHidden)
                    {
                        var uri_path = new Uri(data.GetString(0));
                        var result = new SearchResult
                        {
                            Path = uri_path.LocalPath,
                            Title = data.GetString(1)
                        };
                        _Result.Add(result);
                    }                                 
                }
            }
            return _Result;
        }


        public void ModifyQueryHelper(ref ISearchQueryHelper queryHelper, string pattern)
        {
            // convert file pattern if it is not '*'. Don't create restriction for '*' as it includes all files.
            if (pattern != "*")
            {
                pattern = pattern.Replace("*", "%");
                pattern = pattern.Replace("?", "_");

                if (pattern.Contains("%") || pattern.Contains("_"))
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

        public void InitQueryHelper(out ISearchQueryHelper queryHelper, int maxCount)
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

            // To filter based on title for now
            queryHelper.QueryContentProperties = "System.FileName";

            // Set sorting order 
            queryHelper.QuerySorting = "System.DateModified DESC";
        }

        public IEnumerable<SearchResult> Search(string keyword, string pattern = "*", int maxCount = 100)
        {
            lock(_lock){
                ISearchQueryHelper queryHelper;
                InitQueryHelper(out queryHelper, maxCount);
                ModifyQueryHelper(ref queryHelper, pattern);
                return ExecuteQuery(queryHelper, keyword);
            }
        }
    }
}
