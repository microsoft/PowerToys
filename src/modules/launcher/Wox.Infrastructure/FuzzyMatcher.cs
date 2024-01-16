// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Wox.Infrastructure
{
    [Obsolete("This class is obsolete and should not be used. Please use the static function StringMatcher.FuzzySearch")]
    public class FuzzyMatcher
    {
        private string query;
        private MatchOption opt;

        private FuzzyMatcher(string query, MatchOption opt)
        {
            this.query = query.Trim();
            this.opt = opt;
        }

        public static FuzzyMatcher Create(string query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return new FuzzyMatcher(query, new MatchOption());
        }

        public static FuzzyMatcher Create(string query, MatchOption opt)
        {
            ArgumentNullException.ThrowIfNull(query);

            return new FuzzyMatcher(query, opt);
        }

        public MatchResult Evaluate(string str)
        {
            return StringMatcher.Instance.FuzzyMatch(query, str, opt);
        }
    }
}
