using Microsoft.Plugin.Indexer.Interface;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Windows.Documents;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class WindowsIndexerSearch : ISearch
    {
        public List<DBResults> Query(string connectionString, string sqlQuery)
        {
            List<DBResults> result = new List<DBResults>();

            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                // open the connection
                conn.Open();

                // now create an OleDB command object with the query we built above and the connection we just opened.
                using (OleDbCommand command = new OleDbCommand(sqlQuery, conn))
                {
                    using (DbDataReader WDSResults = command.ExecuteReader())
                    {
                        if (WDSResults.HasRows)
                        {
                            while (WDSResults.Read())
                            {
                                result.Add(new DBResults(WDSResults));
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
