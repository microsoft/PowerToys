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
            if (strings.Length == 1) return; //we consider a valid query must contain a space

            ActionName = strings[0];
            for (int i = 1; i < strings.Length; i++)
            {
                if (!string.IsNullOrEmpty(strings[i]))
                {
                    ActionParameters.Add(strings[i]);
                }
            }
        }
    }
}
