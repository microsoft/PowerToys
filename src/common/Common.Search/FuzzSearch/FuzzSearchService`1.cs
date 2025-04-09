// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.Search;

namespace Common.Search.FuzzSearch;

public class FuzzSearchService<T> : ISearchService<T>
{
    private readonly IEnumerable<T> dataSource;
    private readonly Func<T, string> selector;

    public IEnumerable<T> GetData()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<T> GetSuggestions(string query)
    {
        return dataSource
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

    public FuzzSearchService(IEnumerable<T> dataSource, Func<T, string> selector)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }
}
