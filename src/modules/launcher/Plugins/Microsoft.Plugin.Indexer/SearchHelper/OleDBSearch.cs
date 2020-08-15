// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBSearch : ISearch, IDisposable
    {
        private OleDbCommand command;
        private OleDbConnection conn;
        private OleDbDataReader wDSResults;
        private bool disposedValue;

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Security",
            "CA2100:Review SQL queries for security vulnerabilities",
            Justification = "sqlQuery does not come from user input but is generated via the ISearchQueryHelper::GenerateSqlFromUserQuery see: https://docs.microsoft.com/en-us/windows/win32/search/-search-3x-wds-qryidx-searchqueryhelper#using-the-generatesqlfromuserquery-method")]
        public List<OleDBResult> Query(string connectionString, string sqlQuery)
        {
            List<OleDBResult> result = new List<OleDBResult>();

            using (conn = new OleDbConnection(connectionString))
            {
                // open the connection
                conn.Open();

                try
                {
                    // now create an OleDB command object with the query we built above and the connection we just opened.
                    using (command = new OleDbCommand(sqlQuery, conn))
                    {
                        using (wDSResults = command.ExecuteReader())
                        {
                            if (!wDSResults.IsClosed && wDSResults.HasRows)
                            {
                                while (!wDSResults.IsClosed && wDSResults.Read())
                                {
                                    List<object> fieldData = new List<object>(wDSResults.FieldCount);
                                    for (int i = 0; i < wDSResults.FieldCount; i++)
                                    {
                                        fieldData.Add(wDSResults.GetValue(i));
                                    }

                                    result.Add(new OleDBResult(fieldData));
                                }
                            }
                        }
                    }
                }

                // AccessViolationException can occur if another query is made before the current query completes. Since the old query would be cancelled we can ignore the exception
                catch (System.AccessViolationException)
                {
                    // do nothing
                }
            }

            return result;
        }

        // Checks if all the variables related to database connection have been properly disposed
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
                wDSResults.Read();
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
                    wDSResults?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
