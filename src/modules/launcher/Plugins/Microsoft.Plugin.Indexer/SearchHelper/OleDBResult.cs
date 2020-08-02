using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Text;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBResult
    {
        public List<object> FieldData { get; }

        public OleDBResult(List<object> fieldData)
        {
            FieldData = fieldData;
        }
    }
}
