using Microsoft.Plugin.Indexer.SearchHelper;
using System.Collections.Generic;

namespace Microsoft.Plugin.Indexer.Interface
{
    interface ISearch
    {
        List<DBResults> Query(string connectionString, string sqlQuery);
    }
}
