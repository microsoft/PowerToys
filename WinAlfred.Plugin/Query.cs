using System.Collections.Generic;

namespace WinAlfred.Plugin
{
    public class Query
    {
        public string RawQuery { get; set; }
        public string ActionName { get; private set; }
        public List<string> ActionParameters { get; private set; }

        public Query(string rawQuery)
        {
            RawQuery = rawQuery;
            ParseQuery();
        }

        private void ParseQuery()
        {
            
        }
    }
}
