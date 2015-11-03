using System;
using System.Collections.Generic;
using System.Linq;

namespace Wox.Plugin
{
    public class Query
    {
        /// <summary>
        /// Raw query, this includes action keyword if it has
        /// We didn't recommend use this property directly. You should always use Search property.
        /// </summary>
        public string RawQuery { get; }

        /// <summary>
        /// Search part of a query.
        /// This will not include action keyword if exclusive plugin gets it, otherwise it should be same as RawQuery.
        /// Since we allow user to switch a exclusive plugin to generic plugin, 
        /// so this property will always give you the "real" query part of the query
        /// </summary>
        public string Search { get; internal set; }

        /// <summary>
        /// The raw query splited into a string array.
        /// </summary>
        public string[] Terms { get; }

        public const string Seperater = " ";

        /// <summary>
        /// * is used for System Plugin
        /// </summary>
        public const string WildcardSign = "*";

        internal string ActionKeyword { get; set; }

        /// <summary>
        /// Return first search split by space if it has
        /// </summary>
        public string FirstSearch => SplitSearch(0);

        /// <summary>
        /// strings from second search (including) to last search
        /// </summary>
        public string SecondToEndSearch
        {
            get
            {
                var index = String.IsNullOrEmpty(ActionKeyword) ? 1 : 2;
                return String.Join(Seperater, Terms.Skip(index).ToArray());
            }
        }

        /// <summary>
        /// Return second search split by space if it has
        /// </summary>
        public string SecondSearch => SplitSearch(1);

        /// <summary>
        /// Return third search split by space if it has
        /// </summary>
        public string ThirdSearch => SplitSearch(2);

        private string SplitSearch(int index)
        {
            try
            {
                return String.IsNullOrEmpty(ActionKeyword) ? Terms[index] : Terms[index + 1];
            }
            catch (IndexOutOfRangeException)
            {
                return String.Empty;
            }
        }

        public override string ToString() => RawQuery;

        [Obsolete("Use Search instead, A plugin developer shouldn't care about action name, as it may changed by users. " +
                  "this property will be removed in v1.3.0")]
        public string ActionName { get; private set; }

        [Obsolete("Use Search instead, this property will be removed in v1.3.0")]
        public List<string> ActionParameters { get; private set; }

        public Query(string rawQuery)
        {
            // replace multiple white spaces with one white space
            Terms = rawQuery.Split(new[] { Seperater }, StringSplitOptions.RemoveEmptyEntries);
            RawQuery = String.Join(Seperater, Terms.ToArray());

            ActionParameters = new List<string>();
            ParseQuery();
        }

        private void ParseQuery()
        {
            if (String.IsNullOrEmpty(RawQuery)) return;

            string[] strings = RawQuery.Split(' ');
            //todo:not exactly correct. query that didn't containing a space should be a valid query
            if (strings.Length == 1) return; //we consider a valid query must contain a space

            ActionName = strings[0];
            for (int i = 1; i < strings.Length; i++)
            {
                if (!String.IsNullOrEmpty(strings[i]))
                {
                    ActionParameters.Add(strings[i]);
                }
            }
        }

        [Obsolete("Use Search instead, this method will be removed in v1.3.0")]
        public string GetAllRemainingParameter()
        {

            string[] strings = RawQuery.Split(new char[] { ' ' }, 2, StringSplitOptions.None);
            if (strings.Length > 1)
            {
                return strings[1];
            }

            return String.Empty;
        }


    }
}
