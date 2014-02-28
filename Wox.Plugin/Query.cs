using System.Collections.Generic;

namespace Wox.Plugin
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
            //todo:not exactly correct. query that didn't containing a space should be a valid query
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

        public string GetAllRemainingParameter()
        {
            string[] strings = RawQuery.Split(' ');
            if (strings.Length > 1)
            {
                return RawQuery.Substring(RawQuery.IndexOf(' ') + 1);
            }

            return string.Empty;
        }
    }
}
