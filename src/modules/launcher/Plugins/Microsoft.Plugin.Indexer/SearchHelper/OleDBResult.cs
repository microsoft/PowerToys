using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Text;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class OleDBResult
    {
        public List<object> fieldData;

        public OleDBResult(List<object> fieldData)
        {
            this.fieldData = fieldData;
        }
    }
}
