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
            ActionParameters = new List<string>();
            ParseQuery();
        }

        private void ParseQuery()
        {
            if (string.IsNullOrEmpty(RawQuery)) return;

            string[] strings = RawQuery.Split(' ');
            ActionName = strings[0];
            if (strings.Length > 1)
            {
                for (int i = 1; i < strings.Length; i++)
                {
                    ActionParameters.Add(strings[i]);
                }
            }
        }
    }
}
