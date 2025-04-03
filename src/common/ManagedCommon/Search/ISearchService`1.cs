// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace ManagedCommon.Search;

public interface ISearchService<T>
{
    IEnumerable<T> GetSuggestions(string query);
}

public interface ISearchAlgorithm<T>
{
    IEnumerable<T> GetSuggestions(string query, IEnumerable<T> source, Func<T, string> selector);
}

public class SearchService<T> : ISearchService<T>
{
    private readonly ISearchAlgorithm<T> searchAlgorithm;

    private readonly IEnumerable<T> dataSet;

    private readonly Func<T, string> selector;

    public SearchService(IEnumerable<T> dataSet, ISearchAlgorithm<T> searchAlgorithm, Func<T, string> selector)
    {
        this.dataSet = dataSet ?? throw new ArgumentNullException(nameof(dataSet));
        this.searchAlgorithm = searchAlgorithm ?? throw new ArgumentNullException(nameof(searchAlgorithm));
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public IEnumerable<T> GetSuggestions(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? Enumerable.Empty<T>()
            : searchAlgorithm.GetSuggestions(query, dataSet, selector);
    }
}
