// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ManagedCommon.Search;

/// <summary>
/// A basic search algorithm that matches items whose search field contains the query string, ignoring case.
/// </summary>
public class FuzzSearchAlgorithm<T> : ISearchAlgorithm<T>
{
    public FuzzSearchAlgorithm()
    {
    }

    public IEnumerable<T> GetSuggestions(string query, IEnumerable<T> source, Func<T, string> selector)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Enumerable.Empty<T>();
        }

        return source
            .Select(item =>
            {
                var text = selector(item);
                var match = StringMatcher.FuzzySearch(query, text);
                return new { Item = item, Match = match };
            })
            .Where(x => x.Match.Success)
            .OrderByDescending(x => x.Match.Score)
            .Select(x => x.Item);
    }
}
