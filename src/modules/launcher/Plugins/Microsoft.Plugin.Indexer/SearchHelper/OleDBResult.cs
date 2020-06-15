using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Text;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBResult
    {
        public List<object> fieldData = new List<object>();

        public OleDBResult(OleDbDataReader dbDataReader)
        {
            for (int i = 0; i < dbDataReader.FieldCount; i++)
                fieldData.Add(dbDataReader.GetValue(i));
        }
    }
}
