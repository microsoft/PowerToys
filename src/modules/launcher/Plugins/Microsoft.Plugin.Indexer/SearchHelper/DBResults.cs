using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class DBResults
    {
        object[] data { get; }

        public DBResults(DbDataReader dbDataReader)
        {
            dbDataReader.GetValues(data);
        }
    }
}
