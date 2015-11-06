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
        public string RawQuery { get; internal set; }

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
        public string[] Terms { get; set; }

        /// <summary>
        /// Query can be splited into multiple terms by whitespace
        /// </summary>
        public const string TermSeperater = " ";
        /// <summary>
        /// User can set multiple action keywords seperated by ';'
        /// </summary>
        public const string ActionKeywordSeperater = ";";

        /// <summary>
        /// '*' is used for System Plugin
        /// </summary>
        public const string GlobalPluginWildcardSign = "*";

        public string ActionKeyword { get; set; }

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
                var index = string.IsNullOrEmpty(ActionKeyword) ? 1 : 2;
                return string.Join(TermSeperater, Terms.Skip(index).ToArray());
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
                return string.IsNullOrEmpty(ActionKeyword) ? Terms[index] : Terms[index + 1];
            }
            catch (IndexOutOfRangeException)
            {
                return string.Empty;
            }
        }

        public override string ToString() => RawQuery;

        [Obsolete("Use ActionKeyword, this property will be removed in v1.3.0")]
        public string ActionName { get; internal set; }

        [Obsolete("Use Search instead, this property will be removed in v1.3.0")]
        public List<string> ActionParameters { get; internal set; }

        [Obsolete("Use Search instead, this method will be removed in v1.3.0")]
        public string GetAllRemainingParameter() => Search;
    }
}
