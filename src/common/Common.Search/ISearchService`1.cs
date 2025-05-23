// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

public interface ISearchService<T>
{
    IEnumerable<T> GetData();

    IEnumerable<T> GetSuggestions(string query);
}
