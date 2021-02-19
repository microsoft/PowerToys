// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Collections.Generic;

namespace Wox.Plugin
{
    public class Query
    {
        internal Query()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Query"/> class.
        /// to allow unit tests for plug ins
        /// </summary>
        public Query(string query, string actionKeyword = "")
        {
            _query = query;
            ActionKeyword = actionKeyword;
        }

        private string _rawQuery;

        /// <summary>
        /// Gets raw query, this includes action keyword if it has
        /// We didn't recommend use this property directly. You should always use Search property.
        /// </summary>
        public string RawQuery
        {
            get
            {
                if (_rawQuery == null)
                {
                    _rawQuery = string.Join(Query.TermSeparator, _query.Split(new[] { TermSeparator }, StringSplitOptions.RemoveEmptyEntries));
                }

                return _rawQuery;
            }
        }

        private string _search;

        /// <summary>
        /// Gets search part of a query.
        /// This will not include action keyword if exclusive plugin gets it, otherwise it should be same as RawQuery.
        /// Since we allow user to switch a exclusive plugin to generic plugin,
        /// so this property will always give you the "real" query part of the query
        /// </summary>
        public string Search
        {
            get
            {
                if (_search == null)
                {
                    _search = RawQuery.Substring(ActionKeyword.Length).Trim();
                }

                return _search;
            }
        }

        private ReadOnlyCollection<string> _terms;

        /// <summary>
        /// Gets the raw query splited into a string array.
        /// </summary>
        public ReadOnlyCollection<string> Terms
        {
            get
            {
                if (_terms == null)
                {
                    var terms = _query
                        .Trim()
                        .Substring(ActionKeyword.Length)
                        .Split(new[] { TermSeparator }, StringSplitOptions.RemoveEmptyEntries);

                    _terms = new ReadOnlyCollection<string>(terms);
                }

                return _terms;
            }
        }

        /// <summary>
        /// Query can be splited into multiple terms by whitespace
        /// </summary>
        public const string TermSeparator = " ";

        /// <summary>
        /// User can set multiple action keywords separated by ';'
        /// </summary>
        public const string ActionKeywordSeparator = ";";

        /// <summary>
        /// '*' is used for System Plugin
        /// </summary>
        public const string GlobalPluginWildcardSign = "*";

        public string ActionKeyword { get; set; }

        /// <summary>
        /// Gets return first search split by space if it has
        /// </summary>
        public string FirstSearch => SplitSearch(0);

        /// <summary>
        /// Gets strings from second search (including) to last search
        /// </summary>
        public string SecondToEndSearch
        {
            get
            {
                var index = string.IsNullOrEmpty(ActionKeyword) ? 1 : 2;
                return string.Join(TermSeparator, Terms.Skip(index).ToArray());
            }
        }

        /// <summary>
        /// Gets return second search split by space if it has
        /// </summary>
        public string SecondSearch => SplitSearch(1);

        /// <summary>
        /// Gets return third search split by space if it has
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

        private string _query;

        public override string ToString() => RawQuery;

        [Obsolete("Use Search instead, this method will be removed in v1.3.0")]
        public string GetAllRemainingParameter() => Search;
    }
}
