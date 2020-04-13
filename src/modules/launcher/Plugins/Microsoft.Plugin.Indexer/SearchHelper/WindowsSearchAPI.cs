using System;
using System.Collections.Generic;
using System.Data.OleDb;
using Microsoft.Search.Interop;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class WindowsSearchAPI
    {
        public OleDbConnection conn;
        public OleDbCommand command;
        public OleDbDataReader WDSResults;
        private readonly object _lock = new object();
        

        public List<SearchResult> ExecuteQuery(ISearchQueryHelper queryHelper, string keyword)
        {
            List<SearchResult> _Result = new List<SearchResult>();
            // Generate SQL from our parameters, converting the userQuery from AQS->WHERE clause
            string sqlQuery = queryHelper.GenerateSQLFromUserQuery(keyword);

            // --- Perform the query ---
            // create an OleDbConnection object which connects to the indexer provider with the windows application
            using (conn = new OleDbConnection(queryHelper.ConnectionString))
            {
                // open the connection
                conn.Open();

                // now create an OleDB command object with the query we built above and the connection we just opened.
                using (command = new OleDbCommand(sqlQuery, conn))
                {
                    // execute the command, which returns the results as an OleDbDataReader.
                    using (WDSResults = command.ExecuteReader())
                    {
                        if(WDSResults.HasRows)
                        {
                            while (WDSResults.Read())
                            {
                                if(WDSResults.GetValue(0) != DBNull.Value && WDSResults.GetValue(1) != DBNull.Value)
                                {
                                    var result = new SearchResult
                                    {
                                        Path = WDSResults.GetString(0),
                                        Title = WDSResults.GetString(1)
                                    };
                                    _Result.Add(result);
                                }
                            }
                        }
                    }
                }
            }

            return _Result;
        }

        // To add '*' to the beginning of any search keyword
        public string ModifySearchQuery(string keyword)
        {
            //Presently when we enter a keyword, it is automatically converted to "keyword*"
            //However, to enable search from within a string as well, we need to change the keyword to "*keyword" 
            string modifiedKeyword = "";
            char[] separator = { '*', ' ' };
            string[] elements = keyword.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            foreach(string element in elements)
            {
                modifiedKeyword += "*" + element + " ";
            }
            
            if(modifiedKeyword != "")
            {
                modifiedKeyword = modifiedKeyword.Remove(modifiedKeyword.Length - 1, 1);
            }
            else
            {
                modifiedKeyword = "*";
            }
            
            return modifiedKeyword;
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
            queryHelper.QuerySelectColumns = "System.ItemPathDisplay, System.FileName";

            // Set additional query restriction
            queryHelper.QueryWhereRestrictions = "AND scope='file:'";

            // To filter based on title for now
            queryHelper.QueryContentProperties = "System.FileName";

            // Set sorting order 
            queryHelper.QuerySorting = "System.DateModified DESC";
        }

        public IEnumerable<SearchResult> Search(string keyword, int maxCount = 100)
        {
            lock(_lock){
                ISearchQueryHelper queryHelper;
                InitQueryHelper(out queryHelper, maxCount);
                keyword = ModifySearchQuery(keyword);
                return ExecuteQuery(queryHelper, keyword);
            }
        }
    }
}
