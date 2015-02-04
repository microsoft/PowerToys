using System;
using System.Collections.Generic;

namespace Wox.Plugin
{
    public class Query
    {
        /// <summary>
        /// Raw query, this includes action keyword if it has
        /// We didn't recommend use this property directly. You should always use Search property.
        /// </summary>
        public string RawQuery { get; internal set; }

        /// <summary>
        /// Search part of a query.
        /// This will not include action keyword if regular plugin gets it, and if a system plugin gets it, it should be same as RawQuery.
        /// Since we allow user to switch a regular plugin to system plugin, so this property will always give you the "real" query part of
        /// the query
        /// </summary>
        public string Search { get; internal set; }

        internal string GetActionKeyword()
        {
            if (!string.IsNullOrEmpty(RawQuery))
            {
                var strings = RawQuery.Split(' ');
                if (strings.Length > 0)
                {
                    return strings[0];
                }
            }

            return string.Empty;
        }

        internal bool IsIntantQuery { get; set; }

        /// <summary>
        /// Return first search split by space if it has
        /// </summary>
        public string FirstSearch
        {
            get
            {
                return SplitSearch(0);
            }
        }

        /// <summary>
        /// strings from second search (including) to last search
        /// </summary>
        public string SecondToEndSearch
        {
            get
            {
                if (string.IsNullOrEmpty(Search)) return string.Empty;

                var strings = Search.Split(' ');
                if (strings.Length > 1)
                {
                    return Search.Substring(Search.IndexOf(' ') + 1);
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Return second search split by space if it has
        /// </summary>
        public string SecondSearch
        {
            get
            {
                return SplitSearch(1);
            }
        }

        /// <summary>
        /// Return third search split by space if it has
        /// </summary>
        public string ThirdSearch
        {
            get
            {
                return SplitSearch(2);
            }
        }

        private string SplitSearch(int index)
        {
            if (string.IsNullOrEmpty(Search)) return string.Empty;

            var strings = Search.Split(' ');
            if (strings.Length > index)
            {
                return strings[index];
            }

            return string.Empty;
        }

        public override string ToString()
        {
            return RawQuery;
        }

        [Obsolete("Use Search instead, A plugin developer shouldn't care about action name, as it may changed by users. " +
                  "this property will be removed in v1.3.0")]
        public string ActionName { get; private set; }

        [Obsolete("Use Search instead, this property will be removed in v1.3.0")]
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

        [Obsolete("Use Search instead, this method will be removed in v1.3.0")]
        public string GetAllRemainingParameter()
        {

            string[] strings = RawQuery.Split(new char[] { ' ' }, 2, System.StringSplitOptions.None);
            if (strings.Length > 1)
            {
                return strings[1];
            }

            return string.Empty;
        }
    }
}
