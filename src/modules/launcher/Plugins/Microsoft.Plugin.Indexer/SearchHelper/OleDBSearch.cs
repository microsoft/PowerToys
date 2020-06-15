using Microsoft.Plugin.Indexer.Interface;
using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBSearch : ISearch
    {       

        public List<OleDBResult> Query(string connectionString, string sqlQuery)
        {
            List<OleDBResult> result = new List<OleDBResult>();

            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                // open the connection
                conn.Open();

                // now create an OleDB command object with the query we built above and the connection we just opened.
                using (OleDbCommand command = new OleDbCommand(sqlQuery, conn))
                {
                    using (OleDbDataReader WDSResults = command.ExecuteReader())
                    {
                        if (WDSResults.HasRows)
                        {
                            while (WDSResults.Read())
                            {
                                List<Object> fieldData = new List<object>();
                                for (int i = 0; i < WDSResults.FieldCount; i++)
                                    fieldData.Add(WDSResults.GetValue(i));
                                result.Add(new OleDBResult(fieldData));
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
