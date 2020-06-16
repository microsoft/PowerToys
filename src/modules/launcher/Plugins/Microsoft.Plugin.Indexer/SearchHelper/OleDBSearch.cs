using Microsoft.Plugin.Indexer.Interface;
using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBSearch : ISearch
    {
        private OleDbCommand command;
        private OleDbConnection conn;
        private OleDbDataReader WDSResults;

        public List<OleDBResult> Query(string connectionString, string sqlQuery)
        {
            List<OleDBResult> result = new List<OleDBResult>();

            using (conn = new OleDbConnection(connectionString))
            {
                // open the connection
                conn.Open();

                // now create an OleDB command object with the query we built above and the connection we just opened.
                using (command = new OleDbCommand(sqlQuery, conn))
                {
                    using (WDSResults = command.ExecuteReader())
                    {
                        if (WDSResults.HasRows)
                        {
                            while (WDSResults.Read())
                            {
                                List<Object> fieldData = new List<object>();
                                for (int i = 0; i < WDSResults.FieldCount; i++)
                                {
                                    fieldData.Add(WDSResults.GetValue(i));
                                }
                                result.Add(new OleDBResult(fieldData));
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// Checks if all the variables related to database connection have been properly disposed 
        public bool HaveAllDisposableItemsBeenDisposed()
        {
            bool commandDisposed = false;
            bool connDisposed = false;
            bool resultDisposed = false;

            try
            {
                command.ExecuteReader();
            }
            catch (InvalidOperationException)
            {
                commandDisposed = true;
            }

            try
            {
                WDSResults.Read();
            }
            catch (InvalidOperationException)
            {
                resultDisposed = true;
            }

            if(conn.State == System.Data.ConnectionState.Closed)
            {
                connDisposed = true;
            }

            return commandDisposed && resultDisposed && connDisposed;
        }
    }
}
