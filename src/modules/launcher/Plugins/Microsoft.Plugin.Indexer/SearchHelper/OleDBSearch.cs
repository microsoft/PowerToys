// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBSearch : ISearch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Security",
            "CA2100:Review SQL queries for security vulnerabilities",
            Justification = "sqlQuery does not come from user input but is generated via the ISearchQueryHelper::GenerateSqlFromUserQuery see: https://docs.microsoft.com/en-us/windows/win32/search/-search-3x-wds-qryidx-searchqueryhelper#using-the-generatesqlfromuserquery-method")]
        public List<OleDBResult> Query(string connectionString, string sqlQuery)
        {
            List<OleDBResult> result = new List<OleDBResult>();
            using (var conn = new OleDbConnection(connectionString))
            {
                // open the connection
                conn.Open();

                // now create an OleDB command object with the query we built above and the connection we just opened.
                using (var command = new OleDbCommand(sqlQuery, conn))
                {
                    using (var wDSResults = command.ExecuteReader())
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

            return result;
        }
    }
}
