using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBSearch : ISearch, IDisposable
    {
        private OleDbCommand command;
        private OleDbConnection conn;
        private OleDbDataReader WDSResults;
        private bool disposedValue;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", 
            Justification = "sqlQuery does not come from user input but is generated via the ISearchQueryHelper::GenerateSqlFromUserQuery " +
            " see: https://docs.microsoft.com/en-us/windows/win32/search/-search-3x-wds-qryidx-searchqueryhelper#using-the-generatesqlfromuserquery-method")]
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
                                List<object> fieldData = new List<object>(WDSResults.FieldCount);
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

            if (conn.State == System.Data.ConnectionState.Closed)
            {
                connDisposed = true;
            }

            return commandDisposed && resultDisposed && connDisposed;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    command?.Dispose();
                    conn?.Dispose();
                    WDSResults?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~OleDBSearch()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
