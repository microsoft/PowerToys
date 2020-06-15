using Microsoft.Plugin.Indexer.SearchHelper;
using System.Collections.Generic;

namespace Microsoft.Plugin.Indexer.Interface
{
    interface ISearch
    {
        List<OleDBResult> Query(string connectionString, string sqlQuery);
    }
}
